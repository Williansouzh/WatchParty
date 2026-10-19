using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Application.Features.Videos;
using WatchParty.Infrastructure.Storage;

namespace WatchParty.Tests.Features;

/// <summary>When segments live in a bucket, the HLS playlist served by the API points straight at them.</summary>
public class RemoteStorageTests
{
    [Fact]
    public async Task Playlist_segments_are_rewritten_to_signed_urls_once_offloaded()
    {
        var root = Path.Combine(Path.GetTempPath(), "watchparty-tests", Guid.NewGuid().ToString("N"));
        var remote = new RemoteStub(new LocalVideoStorage(Options.Create(new StorageOptions { RootPath = root })));
        await using var app = new TestApp(services => services.AddSingleton<IVideoStorage>(remote));
        var user = await app.SeedUserAsync();
        var video = await app.SeedVideoAsync(user);

        await app.Db(async db =>
        {
            var storage = remote;
            var folder = storage.CreateFolderKey("hls");
            Directory.CreateDirectory(Path.Combine(storage.GetFolderPath(folder), "v0"));
            await File.WriteAllTextAsync(Path.Combine(storage.GetFolderPath(folder), "master.m3u8"), "#EXTM3U\nv0/index.m3u8\n");
            await File.WriteAllTextAsync(Path.Combine(storage.GetFolderPath(folder), "v0", "index.m3u8"),
                "#EXTM3U\n#EXTINF:6.0,\nseg_0000.ts\n#EXT-X-ENDLIST\n");
            (await db.Videos.FindAsync(video))!.SetHls(folder);
            return await db.SaveChangesAsync();
        });

        var master = await app.Send(new GetHlsFileQuery(video, "master.m3u8"));
        var variant = await app.Send(new GetHlsFileQuery(video, "v0/index.m3u8"));
        var segment = await app.Send(new GetHlsFileQuery(video, "v0/seg_0000.ts"));

        Assert.NotNull(master.Value.PhysicalPath); // master points at other playlists: served as-is
        Assert.Contains("https://bucket.example/", variant.Value.Content);
        Assert.Contains("/v0/seg_0000.ts?signed", variant.Value.Content);
        Assert.Contains("#EXT-X-ENDLIST", variant.Value.Content);
        Assert.StartsWith("https://bucket.example/", segment.Value.RedirectUrl);
        Directory.Delete(root, recursive: true);
    }

    /// <summary>Local storage where every file not on disk is "in the bucket".</summary>
    private sealed class RemoteStub(LocalVideoStorage local) : IVideoStorage
    {
        public Task<StoredFile> SaveAsync(Stream content, string extension, long? maxBytes, CancellationToken ct) => local.SaveAsync(content, extension, maxBytes, ct);
        public string CreateKey(string extension) => local.CreateKey(extension);
        public string GetPhysicalPath(string key) => local.GetPhysicalPath(key);
        public bool Exists(string key) => true;
        public void Delete(string key) => local.Delete(key);
        public string CreateFolderKey(string prefix) => local.CreateFolderKey(prefix);
        public string GetFolderPath(string folderKey) => local.GetFolderPath(folderKey);
        public string? ResolveFolderFile(string folderKey, string relativePath) => local.ResolveFolderFile(folderKey, relativePath);
        public void DeleteFolder(string folderKey) => local.DeleteFolder(folderKey);
        public long? GetAvailableFreeBytes() => local.GetAvailableFreeBytes();
        public MediaLocation? Locate(string key) => local.Locate(key) ?? MediaLocation.Remote($"https://bucket.example/{key}?signed");
        public MediaLocation? LocateInFolder(string folderKey, string relativePath) =>
            local.LocateInFolder(folderKey, relativePath) ?? MediaLocation.Remote($"https://bucket.example/{folderKey}/{relativePath}?signed");
        public Task OffloadAsync(string key, string contentType, CancellationToken ct) => Task.CompletedTask;
        public Task OffloadFolderAsync(string folderKey, Func<string, bool> include, CancellationToken ct) => Task.CompletedTask;
    }
}
