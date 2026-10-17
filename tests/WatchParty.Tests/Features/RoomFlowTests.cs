using FluentValidation;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Application.Features.Chat;
using WatchParty.Application.Features.Rooms;
using WatchParty.Application.Features.Videos;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Videos;

namespace WatchParty.Tests.Features;

public class RoomFlowTests
{
    [Fact]
    public async Task Playback_state_follows_the_server_clock()
    {
        await using var app = new TestApp();
        var user = await app.SeedUserAsync();
        var video = await app.SeedVideoAsync(user);
        var code = (await app.Send(new CreateRoomCommand(user, "Sexta do filme", video))).Value;

        var played = await app.Send(new ControlPlaybackCommand(code, user, PlaybackAction.Play, 120));
        app.Time.Advance(TimeSpan.FromSeconds(30));
        var room = await app.Send(new GetRoomQuery(code.ToLowerInvariant(), user));

        Assert.True(played.Value.IsPlaying);
        Assert.True(room.Value.Playback.IsPlaying);
        Assert.Equal(120, room.Value.Playback.PositionSeconds);
        Assert.Equal(played.Value.UpdatedAtMs + 0, room.Value.Playback.UpdatedAtMs);
        Assert.Equal(video, room.Value.CurrentVideo!.Id);
    }

    [Fact]
    public async Task Rooms_only_accept_ready_videos()
    {
        await using var app = new TestApp();
        var user = await app.SeedUserAsync();
        var processing = await app.SeedVideoAsync(user, ready: false);
        var code = (await app.Send(new CreateRoomCommand(user, "Sala", null))).Value;

        var create = await app.Send(new CreateRoomCommand(user, "Outra", processing));
        var change = await app.Send(new ChangeRoomVideoCommand(code, user, processing));
        var play = await app.Send(new ControlPlaybackCommand(code, user, PlaybackAction.Play, 0));

        Assert.Equal(VideoErrors.NotReady, create.Error);
        Assert.Equal(VideoErrors.NotReady, change.Error);
        Assert.Equal(RoomErrors.NoVideo, play.Error);
    }

    [Fact]
    public async Task Chat_history_is_returned_oldest_first()
    {
        await using var app = new TestApp();
        var user = await app.SeedUserAsync();
        var code = (await app.Send(new CreateRoomCommand(user, "Sala", null))).Value;

        await app.Send(new SendChatMessageCommand(code, user, "primeira"));
        app.Time.Advance(TimeSpan.FromSeconds(1));
        await app.Send(new SendChatMessageCommand(code, user, "  segunda  "));
        var history = await app.Send(new GetChatHistoryQuery(code));

        Assert.Equal(["primeira", "segunda"], history.Value.Select(m => m.Content));
        await Assert.ThrowsAsync<ValidationException>(() => app.Send(new SendChatMessageCommand(code, user, "   ")));
    }

    [Fact]
    public async Task Only_the_host_can_close_a_room()
    {
        await using var app = new TestApp();
        var host = await app.SeedUserAsync("host");
        var guest = await app.SeedUserAsync("guest");
        var code = (await app.Send(new CreateRoomCommand(host, "Sala", null))).Value;
        await app.Send(new SendChatMessageCommand(code, guest, "oi"));

        var byGuest = await app.Send(new DeleteRoomCommand(code, guest));
        var byHost = await app.Send(new DeleteRoomCommand(code, host));

        Assert.Equal(RoomErrors.NotHost, byGuest.Error);
        Assert.True(byHost.IsSuccess);
        Assert.Equal(RoomErrors.NotFound, (await app.Send(new GetChatHistoryQuery(code))).Error);
    }

    [Fact]
    public async Task Room_list_counts_people_online_once_per_user()
    {
        await using var app = new TestApp();
        var user = await app.SeedUserAsync();
        var code = (await app.Send(new CreateRoomCommand(user, "Sala", null))).Value;

        app.Presence.Join(code, new RoomParticipant("conn-phone", user, "Will", "#fff"));
        app.Presence.Join(code, new RoomParticipant("conn-laptop", user, "Will", "#fff"));
        var rooms = await app.Send(new GetRoomsQuery());

        Assert.Equal(1, Assert.Single(rooms).OnlineCount);
    }

    [Fact]
    public async Task Favorites_and_library_filters()
    {
        await using var app = new TestApp();
        var will = await app.SeedUserAsync("will");
        var ana = await app.SeedUserAsync("ana");
        var video = await app.SeedVideoAsync(will);

        var liked = await app.Send(new ToggleFavoriteCommand(video, ana));
        var anaFavorites = await app.Send(new GetLibraryQuery(ana, Filter: LibraryFilter.Favorites));
        var willFavorites = await app.Send(new GetLibraryQuery(will, Filter: LibraryFilter.Favorites));
        var anaUploads = await app.Send(new GetLibraryQuery(ana, Filter: LibraryFilter.MyUploads));
        var search = await app.Send(new GetLibraryQuery(ana, Search: "FIL"));

        Assert.True(liked.Value);
        Assert.True(Assert.Single(anaFavorites).IsFavorite);
        Assert.Empty(willFavorites);
        Assert.Empty(anaUploads);
        Assert.Single(search);
        Assert.False((await app.Send(new ToggleFavoriteCommand(video, ana))).Value);
    }

    [Fact]
    public async Task Only_the_uploader_can_delete_a_video()
    {
        await using var app = new TestApp();
        var will = await app.SeedUserAsync("will");
        var ana = await app.SeedUserAsync("ana");
        var video = await app.SeedVideoAsync(will);

        var result = await app.Send(new UpdateVideoCommand(video, ana, "Hackeado", null));

        Assert.Equal(VideoErrors.NotOwner, result.Error);
    }
}
