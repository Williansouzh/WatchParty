using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using WatchParty.Application.Abstractions.Services;

namespace WatchParty.Infrastructure.Storage;

/// <summary>
/// Stores files flat in a folder on disk. File keys are "{guid}{extension}"; folder keys (HLS renditions)
/// are "{prefix}-{guid}" and hold their own files.
/// </summary>
internal sealed partial class LocalVideoStorage : IVideoStorage
{
    private const int BufferSize = 1024 * 1024;
    private readonly string _root;

    public LocalVideoStorage(IOptions<StorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> SaveAsync(Stream content, string extension, long? maxBytes, CancellationToken cancellationToken)
    {
        var key = CreateKey(extension);
        var path = GetPhysicalPath(key);
        var partialPath = path + ".part";

        try
        {
            long total = 0;
            await using (var file = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            {
                var buffer = new byte[BufferSize];
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (maxBytes is { } max && total > max) throw new FileTooLargeException();
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            File.Move(partialPath, path);
            return new StoredFile(key, total);
        }
        catch
        {
            File.Delete(partialPath);
            throw;
        }
    }

    public string CreateKey(string extension) => $"{Guid.CreateVersion7():N}{extension.ToLowerInvariant()}";

    public string GetPhysicalPath(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || Path.GetFileName(key) != key)
            throw new ArgumentException("Invalid storage key.", nameof(key));

        return Path.Combine(_root, key);
    }

    public bool Exists(string key) => File.Exists(GetPhysicalPath(key));

    public void Delete(string key)
    {
        var path = GetPhysicalPath(key);
        if (File.Exists(path)) File.Delete(path);
    }

    public string CreateFolderKey(string prefix)
    {
        var key = $"{prefix}-{Guid.CreateVersion7():N}";
        Directory.CreateDirectory(GetFolderPath(key));
        return key;
    }

    public string GetFolderPath(string folderKey)
    {
        if (!FolderKeyPattern().IsMatch(folderKey)) throw new ArgumentException("Invalid folder key.", nameof(folderKey));
        return Path.Combine(_root, folderKey);
    }

    public string? ResolveFolderFile(string folderKey, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) || relativePath.Contains("..")) return null;

        var folder = Path.GetFullPath(GetFolderPath(folderKey)) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(folder, relativePath));
        return full.StartsWith(folder, StringComparison.Ordinal) && File.Exists(full) ? full : null;
    }

    public void DeleteFolder(string folderKey)
    {
        var path = GetFolderPath(folderKey);
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    public long? GetAvailableFreeBytes()
    {
        try
        {
            return new DriveInfo(_root).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public MediaLocation? Locate(string key)
    {
        var path = GetPhysicalPath(key);
        return File.Exists(path) ? MediaLocation.Local(path) : null;
    }

    public MediaLocation? LocateInFolder(string folderKey, string relativePath) =>
        ResolveFolderFile(folderKey, relativePath) is { } path ? MediaLocation.Local(path) : null;

    public Task OffloadAsync(string key, string contentType, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task OffloadFolderAsync(string folderKey, Func<string, bool> include, CancellationToken cancellationToken) => Task.CompletedTask;

    [GeneratedRegex("^[a-z]+-[0-9a-f]{32}$")]
    private static partial Regex FolderKeyPattern();
}
