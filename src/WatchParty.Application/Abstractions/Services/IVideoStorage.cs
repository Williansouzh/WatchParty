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

    /// <summary>
    /// Where a finished file can be read from: a local path, or a short-lived URL when it was moved to
    /// remote storage (S3/R2). Null when it doesn't exist.
    /// </summary>
    MediaLocation? Locate(string key);

    /// <summary>Same as <see cref="Locate"/> for a file inside a folder key (HLS segments).</summary>
    MediaLocation? LocateInFolder(string folderKey, string relativePath);

    /// <summary>Moves a finished file to remote storage. Local-only storage does nothing.</summary>
    Task OffloadAsync(string key, string contentType, CancellationToken cancellationToken);

    /// <summary>Moves the files of a folder that match <paramref name="include"/> to remote storage.</summary>
    Task OffloadFolderAsync(string folderKey, Func<string, bool> include, CancellationToken cancellationToken);
}

/// <summary>Exactly one of <see cref="Path"/> (local file) or <see cref="Url"/> (remote) is set.</summary>
public sealed record MediaLocation(string? Path, string? Url)
{
    public static MediaLocation Local(string path) => new(path, null);
    public static MediaLocation Remote(string url) => new(null, url);
}

public sealed record StoredFile(string Key, long SizeBytes);

public sealed class FileTooLargeException() : Exception("The file is larger than the allowed limit.");
