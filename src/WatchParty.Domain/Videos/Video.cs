using WatchParty.Domain.Common;
using WatchParty.Domain.Users;

namespace WatchParty.Domain.Videos;

public sealed class Video : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    private Video() { }

    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public string OriginalFileName { get; private set; } = null!;

    /// <summary>Storage key of the file that is streamed to the players.</summary>
    public string FileKey { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public string? ThumbnailKey { get; private set; }
    public long SizeBytes { get; private set; }
    public double? DurationSeconds { get; private set; }
    public VideoStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public Guid UploadedById { get; private set; }
    public User UploadedBy { get; private set; } = null!;
    public DateTime UploadedAt { get; private set; }

    public static Video Create(
        string title,
        string? description,
        string originalFileName,
        string fileKey,
        string contentType,
        long sizeBytes,
        Guid uploadedById,
        DateTime now)
    {
        var video = new Video
        {
            OriginalFileName = originalFileName,
            FileKey = fileKey,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            UploadedById = uploadedById,
            UploadedAt = now,
            Status = VideoStatus.Processing
        };
        video.UpdateDetails(title, description);
        return video;
    }

    public void UpdateDetails(string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("O título é obrigatório.");

        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void MarkReady(string fileKey, string contentType, long sizeBytes, double? durationSeconds, string? thumbnailKey)
    {
        FileKey = fileKey;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        DurationSeconds = durationSeconds;
        ThumbnailKey = thumbnailKey;
        Status = VideoStatus.Ready;
        FailureReason = null;
    }

    public void MarkFailed(string reason)
    {
        Status = VideoStatus.Failed;
        FailureReason = reason.Length > 500 ? reason[..500] : reason;
    }

    public bool IsOwnedBy(Guid userId) => UploadedById == userId;
}
