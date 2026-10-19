using Microsoft.Extensions.Options;
using WatchParty.Api.Common;

namespace WatchParty.Api.Endpoints;

public static class AppEndpoints
{
    /// <summary>Lets the Android app know a newer APK is available, and serves it.</summary>
    public static IEndpointRouteBuilder MapAppEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/app", (IOptions<MobileAppOptions> options, HttpContext http) =>
        {
            var o = options.Value;
            var hasApk = o.ApkPath is { Length: > 0 } && File.Exists(o.ApkPath);
            var downloadUrl = o.DownloadUrl ?? (hasApk ? $"{http.Request.Scheme}://{http.Request.Host}/download/watchparty.apk" : null);
            return Results.Ok(new { latestBuild = o.LatestBuild, latestVersion = o.LatestVersion, downloadUrl });
        }).AllowAnonymous();

        app.MapGet("/download/watchparty.apk", (IOptions<MobileAppOptions> options) =>
        {
            var path = options.Value.ApkPath;
            return path is { Length: > 0 } && File.Exists(path)
                ? Results.File(Path.GetFullPath(path), "application/vnd.android.package-archive", "WatchParty.apk")
                : Results.NotFound();
        }).AllowAnonymous();

        return app;
    }
}
