using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Domain.Common;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Videos;

public sealed record UploadSubtitleCommand(Guid VideoId, Guid UserId, string Label, string? Language, string FileName, byte[] Content)
    : ICommand<Result<SubtitleDto>>;

public sealed record DeleteSubtitleCommand(Guid VideoId, Guid SubtitleId, Guid UserId) : ICommand<Result>;

public sealed record GetSubtitleFileQuery(Guid VideoId, Guid SubtitleId) : IQuery<Result<MediaFileDto>>;

internal sealed class UploadSubtitleCommandValidator : AbstractValidator<UploadSubtitleCommand>
{
    public const int MaxBytes = 2 * 1024 * 1024;

    public UploadSubtitleCommandValidator()
    {
        RuleFor(c => c.Label).NotEmpty().WithMessage("Dê um nome à legenda (ex: Português).").MaximumLength(Subtitle.LabelMaxLength);
        RuleFor(c => c.Language).MaximumLength(16);
        RuleFor(c => c.Content).Must(c => c.Length is > 0 and <= MaxBytes).WithMessage("A legenda deve ter até 2 MB.");
    }
}

internal sealed class UploadSubtitleCommandHandler(IAppDbContext db, IVideoStorage storage, TimeProvider time)
    : ICommandHandler<UploadSubtitleCommand, Result<SubtitleDto>>
{
    public async Task<Result<SubtitleDto>> Handle(UploadSubtitleCommand command, CancellationToken cancellationToken)
    {
        if (!SubtitleConverter.SupportedExtensions.Contains(Path.GetExtension(command.FileName).ToLowerInvariant()))
            return SubtitleErrors.UnsupportedFormat;
        if (!await db.Videos.AnyAsync(v => v.Id == command.VideoId, cancellationToken)) return VideoErrors.NotFound;

        var vtt = SubtitleConverter.ToWebVtt(command.Content);
        if (vtt is null) return SubtitleErrors.Invalid;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(vtt));
        var stored = await storage.SaveAsync(stream, ".vtt", null, cancellationToken);

        var subtitle = Subtitle.Create(command.VideoId, command.Label, command.Language, stored.Key, command.UserId, time.GetUtcNow().UtcDateTime);
        db.Subtitles.Add(subtitle);
        await db.SaveChangesAsync(cancellationToken);

        return new SubtitleDto(subtitle.Id, subtitle.Label, subtitle.Language, subtitle.UploadedById);
    }
}

internal sealed class DeleteSubtitleCommandHandler(IAppDbContext db, IVideoStorage storage) : ICommandHandler<DeleteSubtitleCommand, Result>
{
    public async Task<Result> Handle(DeleteSubtitleCommand command, CancellationToken cancellationToken)
    {
        var subtitle = await db.Subtitles.FirstOrDefaultAsync(s => s.Id == command.SubtitleId && s.VideoId == command.VideoId, cancellationToken);
        if (subtitle is null) return SubtitleErrors.NotFound;

        var videoOwner = await db.Videos.Where(v => v.Id == command.VideoId).Select(v => v.UploadedById).FirstAsync(cancellationToken);
        if (subtitle.UploadedById != command.UserId && videoOwner != command.UserId) return SubtitleErrors.NotAllowed;

        db.Subtitles.Remove(subtitle);
        await db.SaveChangesAsync(cancellationToken);
        storage.Delete(subtitle.FileKey);
        return Result.Success();
    }
}

internal sealed class GetSubtitleFileQueryHandler(IAppDbContext db, IVideoStorage storage)
    : IQueryHandler<GetSubtitleFileQuery, Result<MediaFileDto>>
{
    public async Task<Result<MediaFileDto>> Handle(GetSubtitleFileQuery query, CancellationToken cancellationToken)
    {
        var key = await db.Subtitles.AsNoTracking()
            .Where(s => s.Id == query.SubtitleId && s.VideoId == query.VideoId)
            .Select(s => s.FileKey)
            .FirstOrDefaultAsync(cancellationToken);

        // Subtitles stay on the server (tiny, and browsers need same-origin for <track>).
        return key is not null && storage.Locate(key) is { Path: { } path }
            ? new MediaFileDto(path, "text/vtt; charset=utf-8")
            : SubtitleErrors.NotFound;
    }
}
