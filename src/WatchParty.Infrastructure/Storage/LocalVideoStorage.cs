using Microsoft.Extensions.Options;
using WatchParty.Application.Abstractions.Services;

namespace WatchParty.Infrastructure.Storage;

/// <summary>Stores files flat in a folder on disk. Keys are "{guid}{extension}".</summary>
internal sealed class LocalVideoStorage : IVideoStorage
{
    private const int BufferSize = 1024 * 1024;
    private readonly string _root;

    public LocalVideoStorage(IOptions<StorageOptions> options)
    {
        _root = Path.GetFullPath(options.Value.RootPath);
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFile> SaveAsync(Stream content, string extension, CancellationToken cancellationToken)
    {
        var key = CreateKey(extension);
        var path = GetPhysicalPath(key);
        var partialPath = path + ".part";

        try
        {
            await using (var file = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            {
                await content.CopyToAsync(file, BufferSize, cancellationToken);
            }

            File.Move(partialPath, path);
            return new StoredFile(key, new FileInfo(path).Length);
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
}
