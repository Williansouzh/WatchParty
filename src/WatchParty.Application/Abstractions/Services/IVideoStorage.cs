namespace WatchParty.Application.Abstractions.Services;

/// <summary>Stores media files. Files are addressed by an opaque key.</summary>
public interface IVideoStorage
{
    Task<StoredFile> SaveAsync(Stream content, string extension, CancellationToken cancellationToken);
    string CreateKey(string extension);
    string GetPhysicalPath(string key);
    bool Exists(string key);
    void Delete(string key);
}

public sealed record StoredFile(string Key, long SizeBytes);
