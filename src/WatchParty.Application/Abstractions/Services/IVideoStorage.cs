namespace WatchParty.Application.Abstractions.Services;

/// <summary>Stores media files. Files are addressed by an opaque key; HLS renditions live in a folder key.</summary>
public interface IVideoStorage
{
    /// <exception cref="FileTooLargeException">The stream had more than <paramref name="maxBytes"/>.</exception>
    Task<StoredFile> SaveAsync(Stream content, string extension, long? maxBytes, CancellationToken cancellationToken);
    string CreateKey(string extension);
    string GetPhysicalPath(string key);
    bool Exists(string key);
    void Delete(string key);

    string CreateFolderKey(string prefix);
    string GetFolderPath(string folderKey);

    /// <summary>Resolves a file inside a folder key, refusing anything that escapes it. Null when missing.</summary>
    string? ResolveFolderFile(string folderKey, string relativePath);
    void DeleteFolder(string folderKey);

    /// <summary>Free space on the disk that holds the media, when it can be determined.</summary>
    long? GetAvailableFreeBytes();
}

public sealed record StoredFile(string Key, long SizeBytes);

public sealed class FileTooLargeException() : Exception("The file is larger than the allowed limit.");
