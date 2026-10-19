using WatchParty.Domain.Common;

namespace WatchParty.Domain.Videos;

/// <summary>A WebVTT subtitle track of a video, extracted from the file or uploaded by someone.</summary>
public sealed class Subtitle : Entity
{
    public const int LabelMaxLength = 60;

    private Subtitle() { }

    public Guid VideoId { get; private set; }
    public string Label { get; private set; } = null!;
    public string? Language { get; private set; }
    public string FileKey { get; private set; } = null!;
    public Guid? UploadedById { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static Subtitle Create(Guid videoId, string label, string? language, string fileKey, Guid? uploadedById, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(label)) throw new DomainException("Dê um nome à legenda.");

        label = label.Trim();
        return new Subtitle
        {
            VideoId = videoId,
            Label = label.Length > LabelMaxLength ? label[..LabelMaxLength] : label,
            Language = string.IsNullOrWhiteSpace(language) ? null : language.Trim().ToLowerInvariant(),
            FileKey = fileKey,
            UploadedById = uploadedById,
            CreatedAt = now
        };
    }
}

public static class SubtitleErrors
{
    public static readonly Error NotFound = Error.NotFound("Subtitle.NotFound", "Legenda não encontrada.");

    public static readonly Error UnsupportedFormat =
        Error.Validation("Subtitle.UnsupportedFormat", "Envie a legenda em .srt ou .vtt.");

    public static readonly Error Invalid =
        Error.Validation("Subtitle.Invalid", "Não consegui ler esse arquivo de legenda.");

    public static readonly Error NotAllowed =
        Error.Forbidden("Subtitle.NotAllowed", "Só quem enviou a legenda ou o dono do vídeo pode apagá-la.");
}
