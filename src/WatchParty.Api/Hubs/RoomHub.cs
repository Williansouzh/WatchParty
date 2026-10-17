using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using WatchParty.Api.Extensions;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Application.Features.Chat;
using WatchParty.Application.Features.Rooms;
using WatchParty.Domain.Rooms;

namespace WatchParty.Api.Hubs;

/// <summary>
/// Real-time side of a room: playback sync, text chat and WebRTC signaling for voice.
/// The video itself never goes through here; each browser streams it from /api/videos/{id}/stream.
/// </summary>
[Authorize]
public sealed class RoomHub(ISender sender, IRoomPresence presence, TimeProvider time) : Hub<IRoomClient>
{
    private const int MaxSignalPayloadLength = 64 * 1024;

    public static string GroupName(string roomCode) => $"room:{roomCode}";

    private Guid UserId => Context.User!.GetUserId();
    private string DisplayName => Context.User!.GetDisplayName();

    public async Task<JoinRoomResponse> JoinRoom(string code)
    {
        await LeaveCurrentRoomAsync();

        var room = (await sender.Send(new GetRoomQuery(code, UserId))).OrThrow();
        var messages = (await sender.Send(new GetChatHistoryQuery(room.Code))).OrThrow();

        var participant = new RoomParticipant(Context.ConnectionId, UserId, DisplayName, Context.User!.GetAvatarColor());
        presence.Join(room.Code, participant);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(room.Code));
        await Clients.OthersInGroup(GroupName(room.Code)).ParticipantJoined(participant);

        return new JoinRoomResponse(
            room with { OnlineCount = presence.CountUsers(room.Code) },
            presence.GetParticipants(room.Code),
            messages,
            Context.ConnectionId,
            NowMs());
    }

    public Task LeaveRoom() => LeaveCurrentRoomAsync();

    public long GetServerTime() => NowMs();

    public async Task<PlaybackStateDto> Playback(PlaybackAction action, double positionSeconds)
    {
        var code = RequireRoom();
        var state = (await sender.Send(new ControlPlaybackCommand(code, UserId, action, positionSeconds))).OrThrow();

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

        await Clients.OthersInGroup(GroupName(code)).VideoChanged(new VideoChangedEvent(changed.Video, changed.Playback, DisplayName));
        return changed;
    }

    public async Task SendMessage(string content)
    {
        var code = RequireRoom();
        var message = (await sender.Send(new SendChatMessageCommand(code, UserId, content))).OrThrow();

        await Clients.Group(GroupName(code)).ChatMessage(message);
    }

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
    public async Task SendVoiceSignal(string targetConnectionId, string type, string payload)
    {
        var code = RequireRoom();
        if (presence.GetRoomOf(targetConnectionId) != code) throw new HubException("Essa pessoa não está na sala.");
        if (payload.Length > MaxSignalPayloadLength) throw new HubException("Sinal de voz muito grande.");

        await Clients.Client(targetConnectionId).VoiceSignal(Context.ConnectionId, type, payload);
    }

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
    }

    private string RequireRoom() =>
        presence.GetRoomOf(Context.ConnectionId) ?? throw new HubException("Você não está em uma sala.");

    private long NowMs() => time.GetUtcNow().ToUnixTimeMilliseconds();
}
