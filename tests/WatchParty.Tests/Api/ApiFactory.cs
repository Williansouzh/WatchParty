using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace WatchParty.Tests.Api;

/// <summary>The real API (endpoints, auth, SignalR) on an in-memory server with a throwaway database.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string InviteCode = "convite-api";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "watchparty-api-tests", Guid.NewGuid().ToString("N"));

    public Dictionary<string, string?> Settings { get; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = $"Data Source={Path.Combine(_root, "test.db")}",
            ["Storage:RootPath"] = Path.Combine(_root, "media"),
            ["Storage:MinFreeBytes"] = "0",
            ["DataProtection:KeysPath"] = Path.Combine(_root, "keys"),
            ["Auth:InviteCode"] = InviteCode,
            ["Ffmpeg:FfprobePath"] = "/nonexistent/ffprobe", // uploads are published as-is, no conversion
            ["Ffmpeg:Hls:Enabled"] = "false",
            ["Backup:Enabled"] = "false",
        }.Concat(Settings)));
    }

    public async Task<(HttpClient Client, string Token, Guid UserId)> RegisterAsync(string userName, string displayName)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/token/register",
            new { userName, displayName, password = "senha123", inviteCode = InviteCode });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await client.GetFromJsonAsync<MeResponse>("/api/auth/me");
        return (client, token, me!.Id);
    }

    public HubConnection CreateHub(string token) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, "/hubs/room"), options =>
            {
                options.Transports = HttpTransportType.LongPolling; // TestServer has no real sockets
                options.HttpMessageHandlerFactory = _ => Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    public sealed record TokenResponse(string AccessToken);
    public sealed record MeResponse(Guid Id, string UserName, string DisplayName);
}
