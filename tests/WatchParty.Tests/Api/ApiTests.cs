using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace WatchParty.Tests.Api;

public class ApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ApiTests(ApiFactory factory) => _factory = factory;

    private static string Unique(string name) => $"{name}{Guid.NewGuid():N}"[..20];

    private static async Task<Guid> UploadAsync(HttpClient client, string title, int bytes = 2048)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(title), "title");
        var file = new ByteArrayContent(new byte[bytes]);
        file.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        form.Add(file, "file", "filme.mp4");
        var response = await client.PostAsync("/api/videos", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task WaitReadyAsync(HttpClient client, Guid videoId)
    {
        for (var i = 0; i < 50; i++)
        {
            var video = await client.GetFromJsonAsync<JsonElement>($"/api/videos/{videoId}");
            if (video.GetProperty("status").GetString() == "Ready") return;
            await Task.Delay(100);
        }
        Assert.Fail("Video never became ready");
    }

    private static async Task<string> CreateRoomAsync(HttpClient client, Guid? videoId)
    {
        var response = await client.PostAsJsonAsync("/api/rooms", new { name = "Sessão", videoId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
    }

    /// <summary>Collects the arguments of a hub event so tests can await the next one.</summary>
    private static Channel<T> Listen<T>(HubConnection hub, string method)
    {
        var channel = Channel.CreateUnbounded<T>();
        hub.On<T>(method, value => channel.Writer.TryWrite(value));
        return channel;
    }

    private static async Task<T> NextAsync<T>(Channel<T> channel, Func<T, bool>? where = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            var value = await channel.Reader.ReadAsync(timeout.Token);
            if (where is null || where(value)) return value;
        }
    }

    [Fact]
    public async Task Signing_out_everywhere_revokes_existing_tokens()
    {
        var (client, token, _) = await _factory.RegisterAsync(Unique("rev"), "Rev");
        var second = _factory.CreateClient();
        second.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("/api/auth/me")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout-everywhere", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/videos")).StatusCode);
    }

    [Fact]
    public async Task Changing_the_password_gives_this_device_a_new_token_and_kills_the_old_one()
    {
        var (client, oldToken, _) = await _factory.RegisterAsync(Unique("pwd"), "Pwd");

        var response = await client.PostAsJsonAsync("/api/auth/password", new { currentPassword = "senha123", newPassword = "outra-senha" });
        response.EnsureSuccessStatusCode();
        var newToken = (await response.Content.ReadFromJsonAsync<ApiFactory.TokenResponse>())!.AccessToken;

        var fresh = _factory.CreateClient();
        fresh.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("/api/auth/me")).StatusCode);
        Assert.NotEqual(oldToken, newToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Room_waits_for_whoever_is_loading_and_resumes_when_everyone_is_ready()
    {
        var (willClient, willToken, _) = await _factory.RegisterAsync(Unique("will"), "Will");
        var (_, anaToken, _) = await _factory.RegisterAsync(Unique("ana"), "Ana");
        var video = await UploadAsync(willClient, "Filme");
        await WaitReadyAsync(willClient, video);
        var code = await CreateRoomAsync(willClient, video);

        await using var will = _factory.CreateHub(willToken);
        await using var ana = _factory.CreateHub(anaToken);
        var willPlayback = Listen<JsonElement>(will, "PlaybackChanged");
        var willWaiting = Listen<string[]>(will, "BufferingChanged");
        await will.StartAsync();
        await ana.StartAsync();
        await will.InvokeAsync<JsonElement>("JoinRoom", code);
        await ana.InvokeAsync<JsonElement>("JoinRoom", code);

        await will.InvokeAsync<JsonElement>("Playback", "Play", 10.0);
        await ana.InvokeAsync("SetBuffering", true);

        var paused = await NextAsync(willPlayback, e => e.GetProperty("reason").ValueKind == JsonValueKind.String);
        Assert.Equal("buffering", paused.GetProperty("reason").GetString());
        Assert.False(paused.GetProperty("state").GetProperty("isPlaying").GetBoolean());
        Assert.Equal(["Ana"], await NextAsync(willWaiting, w => w.Length > 0));

        await ana.InvokeAsync("SetBuffering", false);

        var resumed = await NextAsync(willPlayback, e => e.GetProperty("reason").GetString() == "resumed");
        Assert.True(resumed.GetProperty("state").GetProperty("isPlaying").GetBoolean());
        Assert.Empty(await NextAsync(willWaiting));
        var state = await will.InvokeAsync<JsonElement>("GetPlayback");
        Assert.True(state.GetProperty("isPlaying").GetBoolean());
    }

    [Fact]
    public async Task Deleting_the_video_tells_the_room_and_reactions_are_broadcast()
    {
        var (client, token, _) = await _factory.RegisterAsync(Unique("del"), "Del");
        var video = await UploadAsync(client, "Vai sumir");
        await WaitReadyAsync(client, video);
        var code = await CreateRoomAsync(client, video);

        await using var hub = _factory.CreateHub(token);
        var removed = Listen<JsonElement>(hub, "VideoRemoved");
        var reactions = Listen<JsonElement>(hub, "Reaction");
        await hub.StartAsync();
        await hub.InvokeAsync<JsonElement>("JoinRoom", code);

        await hub.InvokeAsync("SendReaction", "🔥");
        Assert.Equal("🔥", (await NextAsync(reactions)).GetProperty("emoji").GetString());
        await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync("SendReaction", "💣"));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/videos/{video}")).StatusCode);
        Assert.Equal(video, (await NextAsync(removed)).GetProperty("videoId").GetGuid());
    }

    [Fact]
    public async Task Lobby_hears_about_new_rooms()
    {
        var (_, watcherToken, _) = await _factory.RegisterAsync(Unique("lob"), "Lob");
        var (creator, _, _) = await _factory.RegisterAsync(Unique("cre"), "Criadora");

        await using var hub = _factory.CreateHub(watcherToken);
        var activity = Listen<JsonElement>(hub, "RoomActivity");
        await hub.StartAsync();
        await hub.InvokeAsync("WatchLobby");

        var code = await CreateRoomAsync(creator, null);

        var created = await NextAsync(activity, a => a.GetProperty("code").GetString() == code);
        Assert.Equal("created", created.GetProperty("kind").GetString());
        Assert.Equal("Criadora", created.GetProperty("byDisplayName").GetString());
    }

    [Fact]
    public async Task Hls_paths_cannot_escape_the_video_folder_and_health_is_reported()
    {
        var (client, _, _) = await _factory.RegisterAsync(Unique("hls"), "Hls");
        var video = await UploadAsync(client, "Sem HLS");

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/videos/{video}/hls/..%2F..%2Ftest.db")).StatusCode);

        var health = await _factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        var body = await health.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Healthy", body.GetProperty("checks").GetProperty("database").GetProperty("status").GetString());
    }
}
