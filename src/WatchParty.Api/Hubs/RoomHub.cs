using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using WatchParty.Api.Common;
using WatchParty.Api.Extensions;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Application.Features.Chat;
using WatchParty.Application.Features.Rooms;
using WatchParty.Domain.Rooms;

namespace WatchParty.Api.Hubs;

/// <summary>
/// Real-time side of a room: playback sync, waiting for whoever is loading, queue, chat, reactions and
/// WebRTC signaling for voice. The video itself never goes through here; players stream it over HTTP.
/// </summary>
[Authorize]
public sealed class RoomHub(ISender sender, IRoomPresence presence, TimeProvider time, IOptions<VoiceOptions> voiceOptions) : Hub<IRoomClient>
{
    public const string LobbyGroup = "lobby";
    private const int MaxSignalPayloadLength = 64 * 1024;
    private static readonly HashSet<string> AllowedReactions = ["😂", "😍", "🔥", "😱", "👏", "😭", "👍", "🍿"];

    public static string GroupName(string roomCode) => $"room:{roomCode}";

    private Guid UserId => Context.User!.GetUserId();
    private string DisplayName => Context.User!.GetDisplayName();

    // ---------- Room membership ----------

    public async Task<JoinRoomResponse> JoinRoom(string code)
    {
        await LeaveCurrentRoomAsync();

        var room = (await sender.Send(new GetRoomQuery(code, UserId))).OrThrow();
        var messages = (await sender.Send(new GetChatHistoryQuery(room.Code))).OrThrow();
        var wasEmpty = presence.CountUsers(room.Code) == 0;

        var participant = new RoomParticipant(Context.ConnectionId, UserId, DisplayName, Context.User!.GetAvatarColor());
        presence.Join(room.Code, participant);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(room.Code));
        await Clients.OthersInGroup(GroupName(room.Code)).ParticipantJoined(participant);

        if (wasEmpty)
        {
            await Clients.Group(LobbyGroup).RoomActivity(
                new RoomActivityEvent("started", room.Code, room.Name, room.CurrentVideo?.Title, UserId, DisplayName));
        }

        return new JoinRoomResponse(
            room with { OnlineCount = presence.CountUsers(room.Code) },
            presence.GetParticipants(room.Code),
            messages,
            Context.ConnectionId,
            NowMs(),
            WaitingFor(room.Code));
    }

    public Task LeaveRoom() => LeaveCurrentRoomAsync();

    /// <summary>Receive <see cref="IRoomClient.RoomActivity"/> while browsing outside rooms.</summary>
    public Task WatchLobby() => Groups.AddToGroupAsync(Context.ConnectionId, LobbyGroup);

    public Task UnwatchLobby() => Groups.RemoveFromGroupAsync(Context.ConnectionId, LobbyGroup);

    public long GetServerTime() => NowMs();

    // ---------- Playback ----------

    public async Task<PlaybackStateDto> Playback(PlaybackAction action, double positionSeconds)
    {
        var code = RequireRoom();
        var state = (await sender.Send(new ControlPlaybackCommand(code, UserId, action, positionSeconds))).OrThrow();

        // Someone pressing play overrides the automatic wait.
        if (action == PlaybackAction.Play && presence.IsAutoPaused(code))
        {
            presence.SetAutoPaused(code, false);
            await Clients.Group(GroupName(code)).BufferingChanged([]);
        }

        await Clients.OthersInGroup(GroupName(code)).PlaybackChanged(new PlaybackEvent(state, action, DisplayName));
        return state;
    }

    public async Task<PlaybackStateDto> GetPlayback()
    {
        var code = RequireRoom();
        return (await sender.Send(new GetPlaybackQuery(code))).OrThrow();
    }

    public async Task<RoomVideoChangedDto> ChangeVideo(Guid videoId)
    {
        var code = RequireRoom();
        var changed = (await sender.Send(new ChangeRoomVideoCommand(code, UserId, videoId))).OrThrow();
        presence.SetAutoPaused(code, false);

        await Clients.OthersInGroup(GroupName(code))
            .VideoChanged(new VideoChangedEvent(changed.Video, changed.Playback, DisplayName, changed.Queue));
        return changed;
    }

    /// <summary>
    /// A player reports it has been stuck loading (or recovered). While anyone is loading the room waits
    /// (auto-pause); when everyone is ready it resumes by itself.
    /// </summary>
    public async Task SetBuffering(bool buffering)
    {
        var code = RequireRoom();
        var before = presence.Get(Context.ConnectionId);
        if (before is null || before.IsBuffering == buffering) return;

        var me = presence.Update(Context.ConnectionId, p => p with { IsBuffering = buffering })!;
        await Clients.OthersInGroup(GroupName(code)).ParticipantUpdated(me);

        if (buffering)
        {
            var state = (await sender.Send(new GetPlaybackQuery(code))).OrThrow();
            if (state.IsPlaying)
            {
                var position = state.PositionSeconds + Math.Max(0, (NowMs() - state.UpdatedAtMs) / 1000d);
                var paused = (await sender.Send(new ControlPlaybackCommand(code, UserId, PlaybackAction.Pause, position, IsSystem: true))).OrThrow();
                presence.SetAutoPaused(code, true);
                await Clients.Group(GroupName(code)).PlaybackChanged(new PlaybackEvent(paused, PlaybackAction.Pause, DisplayName, "buffering"));
            }
        }
        else
        {
            await ResumeIfEveryoneReadyAsync(code);
        }

        if (presence.IsAutoPaused(code)) await Clients.Group(GroupName(code)).BufferingChanged(WaitingFor(code));
    }

    // ---------- Queue ----------

    public async Task<IReadOnlyList<QueueItemDto>> AddToQueue(Guid videoId)
    {
        var code = RequireRoom();
        var queue = (await sender.Send(new AddToQueueCommand(code, UserId, videoId))).OrThrow();
        await Clients.OthersInGroup(GroupName(code)).QueueChanged(queue);
        return queue;
    }

    public async Task<IReadOnlyList<QueueItemDto>> AddPlaylistToQueue(Guid playlistId)
    {
        var code = RequireRoom();
        var queue = (await sender.Send(new AddPlaylistToQueueCommand(code, UserId, playlistId))).OrThrow();
        await Clients.OthersInGroup(GroupName(code)).QueueChanged(queue);
        return queue;
    }

    public async Task<IReadOnlyList<QueueItemDto>> RemoveFromQueue(Guid itemId)
    {
        var code = RequireRoom();
        var queue = (await sender.Send(new RemoveFromQueueCommand(code, UserId, itemId))).OrThrow();
        await Clients.OthersInGroup(GroupName(code)).QueueChanged(queue);
        return queue;
    }

    /// <summary>Skips to the next queued video.</summary>
    public Task PlayNext() => AdvanceAsync(null);

    /// <summary>Players call this when the video ends; the queue advances once even if everyone reports it.</summary>
    public Task VideoEnded(Guid videoId) => AdvanceAsync(videoId);

    private async Task AdvanceAsync(Guid? finishedVideoId)
    {
        var code = RequireRoom();
        var changed = (await sender.Send(new PlayNextCommand(code, UserId, finishedVideoId))).OrThrow();
        if (changed is null) return;

        presence.SetAutoPaused(code, false);
        await Clients.Group(GroupName(code)).VideoChanged(
            new VideoChangedEvent(changed.Video, changed.Playback, DisplayName, changed.Queue, "next"));
    }

    // ---------- Settings, chat, reactions ----------

    public async Task SetHostOnlyControl(bool hostOnly)
    {
        var code = RequireRoom();
        (await sender.Send(new SetHostOnlyControlCommand(code, UserId, hostOnly))).OrThrow();
        await Clients.Group(GroupName(code)).RoomSettingsChanged(new RoomSettingsEvent(hostOnly, DisplayName));
    }

    public async Task SendMessage(string content)
    {
        var code = RequireRoom();
        var message = (await sender.Send(new SendChatMessageCommand(code, UserId, content))).OrThrow();

        await Clients.Group(GroupName(code)).ChatMessage(message);
    }

    public async Task SendReaction(string emoji)
    {
        var code = RequireRoom();
        if (!AllowedReactions.Contains(emoji)) throw new HubException("Reação inválida.");
        if (!presence.TryThrottle(Context.ConnectionId, "reaction", TimeSpan.FromMilliseconds(300))) return;

        await Clients.Group(GroupName(code)).Reaction(new ReactionEvent(emoji, DisplayName, Context.ConnectionId));
    }

    // ---------- Voice ----------

    /// <summary>Joins the voice call. Returns who is already in it; the caller sends each of them an offer.</summary>
    public async Task<IReadOnlyList<RoomParticipant>> JoinVoice()
    {
        var code = RequireRoom();
        var me = presence.Update(Context.ConnectionId, p => p with { InVoice = true, IsMuted = false })!;
        await Clients.OthersInGroup(GroupName(code)).ParticipantUpdated(me);

        return presence.GetParticipants(code).Where(p => p.InVoice && p.ConnectionId != Context.ConnectionId).ToList();
    }

    public async Task LeaveVoice()
    {
        var code = RequireRoom();
        var me = presence.Update(Context.ConnectionId, p => p with { InVoice = false, IsMuted = false })!;
        await Clients.OthersInGroup(GroupName(code)).ParticipantUpdated(me);
    }

    public async Task SetMuted(bool muted)
    {
        var code = RequireRoom();
        var me = presence.Update(Context.ConnectionId, p => p with { IsMuted = muted })!;
        await Clients.OthersInGroup(GroupName(code)).ParticipantUpdated(me);
    }

    /// <summary>Relays WebRTC offers/answers/ICE candidates between two people in the same room.</summary>
    /// <summary>
    /// LiveKit mode only: a token that lets this connection join the room's LiveKit call. The LiveKit
    /// identity is the SignalR connection id, so speaking indicators map straight onto participants.
    /// </summary>
    public string GetVoiceToken()
    {
        var code = RequireRoom();
        var voice = voiceOptions.Value;
        if (!voice.UseLiveKit) throw new HubException("O servidor não está configurado para usar LiveKit.");

        return LiveKitToken.Create(voice.LiveKit, $"watchparty-{code}", Context.ConnectionId, DisplayName, time.GetUtcNow());
    }

    public async Task SendVoiceSignal(string targetConnectionId, string type, string payload)
    {
        var code = RequireRoom();
        if (presence.GetRoomOf(targetConnectionId) != code) throw new HubException("Essa pessoa não está na sala.");
        if (payload.Length > MaxSignalPayloadLength) throw new HubException("Sinal de voz muito grande.");

        await Clients.Client(targetConnectionId).VoiceSignal(Context.ConnectionId, type, payload);
    }

    // ---------- helpers ----------

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await LeaveCurrentRoomAsync();
        await base.OnDisconnectedAsync(exception);
    }

    private async Task LeaveCurrentRoomAsync()
    {
        if (presence.Leave(Context.ConnectionId) is not { } left) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(left.RoomCode));
        await Clients.Group(GroupName(left.RoomCode)).ParticipantLeft(Context.ConnectionId);

        // If the room was waiting for this person, don't keep everyone waiting.
        if (left.Participant.IsBuffering) await ResumeIfEveryoneReadyAsync(left.RoomCode);
    }

    private async Task ResumeIfEveryoneReadyAsync(string code)
    {
        if (!presence.IsAutoPaused(code) || WaitingFor(code).Count > 0) return;

        presence.SetAutoPaused(code, false);
        await Clients.Group(GroupName(code)).BufferingChanged([]);

        var state = (await sender.Send(new GetPlaybackQuery(code))).OrThrow();
        if (state.IsPlaying || state.VideoId is null) return;

        var resumed = (await sender.Send(new ControlPlaybackCommand(code, UserId, PlaybackAction.Play, state.PositionSeconds, IsSystem: true))).OrThrow();
        await Clients.Group(GroupName(code)).PlaybackChanged(new PlaybackEvent(resumed, PlaybackAction.Play, "WatchParty", "resumed"));
    }

    private IReadOnlyList<string> WaitingFor(string code) =>
        presence.GetParticipants(code).Where(p => p.IsBuffering).Select(p => p.DisplayName).Distinct().ToList();

    private string RequireRoom() =>
        presence.GetRoomOf(Context.ConnectionId) ?? throw new HubException("Você não está em uma sala.");

    private long NowMs() => time.GetUtcNow().ToUnixTimeMilliseconds();
}
