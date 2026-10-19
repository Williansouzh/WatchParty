using System.Text;
using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Application.Features.Auth;
using WatchParty.Application.Features.Maintenance;
using WatchParty.Application.Features.Playlists;
using WatchParty.Application.Features.Rooms;
using WatchParty.Application.Features.Videos;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Users;
using WatchParty.Domain.Videos;

namespace WatchParty.Tests.Features;

public class UploadLimitTests
{
    private static Stream Bytes(int count) => new MemoryStream(new byte[count]);

    [Fact]
    public async Task Files_over_the_limit_are_refused_even_when_the_size_is_not_declared()
    {
        await using var app = new TestApp(settings: new() { ["Storage:MaxUploadBytes"] = "1000", ["Storage:MinFreeBytes"] = "0" });
        var user = await app.SeedUserAsync();

        var declared = await app.Send(new UploadVideoCommand(user, "Filme", null, "a.mp4", Bytes(10), DeclaredLength: 5000));
        var streamed = await app.Send(new UploadVideoCommand(user, "Filme", null, "a.mp4", Bytes(5000)));
        var ok = await app.Send(new UploadVideoCommand(user, "Filme", null, "a.mp4", Bytes(500)));

        Assert.Equal(VideoErrors.TooLarge, declared.Error);
        Assert.Equal(VideoErrors.TooLarge, streamed.Error);
        Assert.True(ok.IsSuccess);
        Assert.Single(app.Queue.Enqueued);
        Assert.Single(Directory.GetFiles(app.MediaRoot)); // the refused upload left nothing behind
    }

    [Fact]
    public async Task Each_person_has_a_quota()
    {
        await using var app = new TestApp(settings: new() { ["Storage:UserQuotaBytes"] = "1000", ["Storage:MinFreeBytes"] = "0" });
        var will = await app.SeedUserAsync("will");
        var ana = await app.SeedUserAsync("ana");

        Assert.True((await app.Send(new UploadVideoCommand(will, "1", null, "a.mp4", Bytes(800)))).IsSuccess);
        Assert.Equal(VideoErrors.QuotaExceeded, (await app.Send(new UploadVideoCommand(will, "2", null, "a.mp4", Bytes(300)))).Error);
        Assert.True((await app.Send(new UploadVideoCommand(ana, "3", null, "a.mp4", Bytes(800)))).IsSuccess);
    }

    [Fact]
    public async Task Uploads_stop_before_the_disk_fills_up()
    {
        await using var app = new TestApp(settings: new() { ["Storage:MinFreeBytes"] = long.MaxValue.ToString() });
        var user = await app.SeedUserAsync();

        var result = await app.Send(new UploadVideoCommand(user, "Filme", null, "a.mp4", Bytes(10)));

        Assert.Equal(VideoErrors.DiskFull, result.Error);
    }
}

public class SessionTests
{
    [Fact]
    public async Task Changing_the_password_rotates_the_security_stamp()
    {
        await using var app = new TestApp();
        var registered = await app.Send(new RegisterUserCommand("will", "Willian", "senha123", TestApp.InviteCode));
        var userId = registered.Value.User.Id;

        var wrong = await app.Send(new ChangePasswordCommand(userId, "errada", "nova-senha"));
        var changed = await app.Send(new ChangePasswordCommand(userId, "senha123", "nova-senha"));

        Assert.Equal(UserErrors.WrongPassword, wrong.Error);
        Assert.NotEqual(registered.Value.SecurityStamp, changed.Value.SecurityStamp);
        Assert.Equal(changed.Value.SecurityStamp, await app.Send(new GetSecurityStampQuery(userId)));
        Assert.True((await app.Send(new LoginCommand("will", "nova-senha"))).IsSuccess);
        Assert.Equal(UserErrors.InvalidCredentials, (await app.Send(new LoginCommand("will", "senha123"))).Error);
    }

    [Fact]
    public async Task Signing_out_everywhere_rotates_the_stamp()
    {
        await using var app = new TestApp();
        var session = (await app.Send(new RegisterUserCommand("will", "Willian", "senha123", TestApp.InviteCode))).Value;

        await app.Send(new SignOutEverywhereCommand(session.User.Id));

        Assert.NotEqual(session.SecurityStamp, await app.Send(new GetSecurityStampQuery(session.User.Id)));
    }
}

public class SubtitleTests
{
    [Fact]
    public void Srt_becomes_webvtt_and_latin1_is_understood()
    {
        var srt = "1\r\n00:00:01,500 --> 00:00:03,000\r\nOlá, coração!\r\n";
        var vtt = SubtitleConverter.ToWebVtt(Encoding.Latin1.GetBytes(srt));

        Assert.NotNull(vtt);
        Assert.StartsWith("WEBVTT\n\n", vtt);
        Assert.Contains("00:00:01.500 --> 00:00:03.000", vtt);
        Assert.Contains("Olá, coração!", vtt);
        Assert.Null(SubtitleConverter.ToWebVtt("not a subtitle"u8.ToArray()));
    }

    [Fact]
    public async Task Upload_list_and_delete_subtitles()
    {
        await using var app = new TestApp();
        var owner = await app.SeedUserAsync("owner");
        var friend = await app.SeedUserAsync("friend");
        var stranger = await app.SeedUserAsync("stranger");
        var video = await app.SeedVideoAsync(owner);
        var srt = Encoding.UTF8.GetBytes("1\n00:00:01,000 --> 00:00:02,000\nOi\n");

        var bad = await app.Send(new UploadSubtitleCommand(video, friend, "PT", "pt", "legenda.txt", srt));
        var added = await app.Send(new UploadSubtitleCommand(video, friend, "Português", "pt", "legenda.srt", srt));
        var file = await app.Send(new GetSubtitleFileQuery(video, added.Value.Id));
        var listed = await app.Send(new GetVideoQuery(video, owner));

        Assert.Equal(SubtitleErrors.UnsupportedFormat, bad.Error);
        Assert.StartsWith("WEBVTT", await File.ReadAllTextAsync(file.Value.PhysicalPath));
        Assert.Equal("Português", Assert.Single(listed.Value.Subtitles).Label);
        Assert.Equal(SubtitleErrors.NotAllowed, (await app.Send(new DeleteSubtitleCommand(video, added.Value.Id, stranger))).Error);
        Assert.True((await app.Send(new DeleteSubtitleCommand(video, added.Value.Id, owner))).IsSuccess);
        Assert.False(File.Exists(file.Value.PhysicalPath));
    }
}

public class QueueAndHostTests
{
    [Fact]
    public async Task Queue_advances_once_when_every_player_reports_the_end()
    {
        await using var app = new TestApp();
        var user = await app.SeedUserAsync();
        var first = await app.SeedVideoAsync(user);
        var second = await app.SeedVideoAsync(user);
        var code = (await app.Send(new CreateRoomCommand(user, "Sala", first))).Value;
        await app.Send(new AddToQueueCommand(code, user, second));

        var advanced = await app.Send(new PlayNextCommand(code, user, FinishedVideoId: first));
        var duplicate = await app.Send(new PlayNextCommand(code, user, FinishedVideoId: first));
        var room = (await app.Send(new GetRoomQuery(code, user))).Value;

        Assert.Equal(second, advanced.Value!.Video.Id);
        Assert.True(advanced.Value.Playback.IsPlaying);
        Assert.Null(duplicate.Value);
        Assert.Equal(second, room.CurrentVideo!.Id);
        Assert.Empty(room.Queue);
        Assert.Equal(RoomErrors.QueueEmpty, (await app.Send(new PlayNextCommand(code, user))).Error);
    }

    [Fact]
    public async Task A_room_can_start_from_a_playlist()
    {
        await using var app = new TestApp();
        var user = await app.SeedUserAsync();
        var a = await app.SeedVideoAsync(user);
        var b = await app.SeedVideoAsync(user);
        var processing = await app.SeedVideoAsync(user, ready: false);
        var playlist = (await app.Send(new CreatePlaylistCommand(user, "Maratona", null))).Value;
        foreach (var v in new[] { a, processing, b }) await app.Send(new AddVideoToPlaylistCommand(playlist, v, user));

        var code = (await app.Send(new CreateRoomCommand(user, "Maratona", null, playlist))).Value;
        var room = (await app.Send(new GetRoomQuery(code, user))).Value;

        Assert.Equal(a, room.CurrentVideo!.Id);
        Assert.Equal([b], room.Queue.Select(q => q.Video.Id));
    }

    [Fact]
    public async Task Host_only_rooms_reject_other_people_but_not_automatic_actions()
    {
        await using var app = new TestApp();
        var host = await app.SeedUserAsync("host");
        var guest = await app.SeedUserAsync("guest");
        var video = await app.SeedVideoAsync(host);
        var other = await app.SeedVideoAsync(host);
        var code = (await app.Send(new CreateRoomCommand(host, "Sala", video))).Value;

        Assert.Equal(RoomErrors.NotHost, (await app.Send(new SetHostOnlyControlCommand(code, guest, true))).Error);
        Assert.True((await app.Send(new SetHostOnlyControlCommand(code, host, true))).IsSuccess);

        Assert.Equal(RoomErrors.HostOnly, (await app.Send(new ControlPlaybackCommand(code, guest, PlaybackAction.Play, 0))).Error);
        Assert.Equal(RoomErrors.HostOnly, (await app.Send(new ChangeRoomVideoCommand(code, guest, other))).Error);
        Assert.True((await app.Send(new ControlPlaybackCommand(code, guest, PlaybackAction.Pause, 5, IsSystem: true))).IsSuccess);
        Assert.True((await app.Send(new ControlPlaybackCommand(code, host, PlaybackAction.Play, 5))).IsSuccess);

        // Guests can still suggest videos and remove their own suggestions.
        var queue = (await app.Send(new AddToQueueCommand(code, guest, other))).Value;
        Assert.True((await app.Send(new RemoveFromQueueCommand(code, guest, queue[0].ItemId))).IsSuccess);
    }

    [Fact]
    public async Task Deleting_a_video_reports_the_rooms_that_were_playing_it()
    {
        await using var app = new TestApp();
        var user = await app.SeedUserAsync();
        var video = await app.SeedVideoAsync(user);
        var code = (await app.Send(new CreateRoomCommand(user, "Sala", video))).Value;

        var deleted = await app.Send(new DeleteVideoCommand(video, user));
        var room = (await app.Send(new GetRoomQuery(code, user))).Value;

        Assert.Equal([code], deleted.Value);
        Assert.Null(room.CurrentVideo);
    }
}

public class MaintenanceTests
{
    [Fact]
    public async Task Cleanup_removes_idle_empty_rooms_and_old_messages()
    {
        await using var app = new TestApp(settings: new() { ["Maintenance:RoomInactiveDays"] = "30", ["Maintenance:ChatRetentionDays"] = "90" });
        var user = await app.SeedUserAsync();
        var idle = (await app.Send(new CreateRoomCommand(user, "Velha", null))).Value;
        var occupied = (await app.Send(new CreateRoomCommand(user, "Ocupada", null))).Value;
        await app.Send(new Application.Features.Chat.SendChatMessageCommand(occupied, user, "mensagem antiga"));
        app.Presence.Join(occupied, new RoomParticipant("c1", user, "Will", "#fff"));

        app.Time.Advance(TimeSpan.FromDays(100));
        var fresh = (await app.Send(new CreateRoomCommand(user, "Nova", null))).Value;
        var result = await app.Send(new CleanupCommand());

        Assert.Equal(1, result.RoomsDeleted);
        Assert.Equal(1, result.MessagesDeleted);
        var codes = await app.Db(db => db.Rooms.Select(r => r.Code).ToListAsync());
        Assert.DoesNotContain(idle, codes);
        Assert.Contains(occupied, codes);
        Assert.Contains(fresh, codes);
    }
}
