using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Domain.Common;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Videos;

/// <param name="DeclaredLength">Size announced by the client (Content-Length), used to refuse early.</param>
public sealed record UploadVideoCommand(
    Guid UserId,
    string Title,
    string? Description,
    string FileName,
    Stream Content,
    long? DeclaredLength = null) : ICommand<Result<Guid>>;

internal sealed class UploadVideoCommandValidator : AbstractValidator<UploadVideoCommand>
{
    public UploadVideoCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().WithMessage("Informe um título.").MaximumLength(Video.TitleMaxLength);
        RuleFor(c => c.Description).MaximumLength(Video.DescriptionMaxLength);
        RuleFor(c => c.FileName)
            .NotEmpty()
            .Must(VideoFormats.IsSupported)
            .WithMessage($"Formato não suportado. Use: {string.Join(", ", VideoFormats.SupportedExtensions)}.");
    }
}

internal sealed class UploadVideoCommandHandler(
    IAppDbContext db,
    IVideoStorage storage,
    IVideoProcessingQueue processingQueue,
    IOptions<StorageLimitsOptions> limits,
    TimeProvider time) : ICommandHandler<UploadVideoCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UploadVideoCommand command, CancellationToken cancellationToken)
    {
        var options = limits.Value;
        var declared = command.DeclaredLength ?? 0;

        if (options.MaxUploadBytes > 0 && declared > options.MaxUploadBytes) return VideoErrors.TooLarge;

        var used = await db.Videos.Where(v => v.UploadedById == command.UserId).SumAsync(v => (long?)v.SizeBytes, cancellationToken) ?? 0;
        if (options.UserQuotaBytes > 0 && used + declared > options.UserQuotaBytes) return VideoErrors.QuotaExceeded;

        if (storage.GetAvailableFreeBytes() is { } free && free - declared < options.MinFreeBytes) return VideoErrors.DiskFull;

        // The declared size can lie (or be missing), so the limit is also enforced while reading.
        long? maxBytes = options.MaxUploadBytes > 0 ? options.MaxUploadBytes : null;
        if (options.UserQuotaBytes > 0) maxBytes = Math.Min(maxBytes ?? long.MaxValue, options.UserQuotaBytes - used);

        var extension = Path.GetExtension(command.FileName).ToLowerInvariant();
        StoredFile stored;
        try
        {
            stored = await storage.SaveAsync(command.Content, extension, maxBytes, cancellationToken);
        }
        catch (FileTooLargeException)
        {
            return options.MaxUploadBytes > 0 && maxBytes == options.MaxUploadBytes ? VideoErrors.TooLarge : VideoErrors.QuotaExceeded;
        }

        try
        {
            var video = Video.Create(
                command.Title,
                command.Description,
                Path.GetFileName(command.FileName),
                stored.Key,
                VideoFormats.GetContentType(command.FileName),
                stored.SizeBytes,
                command.UserId,
                time.GetUtcNow().UtcDateTime);

            db.Videos.Add(video);
            await db.SaveChangesAsync(cancellationToken);

            processingQueue.Enqueue(video.Id);
            return video.Id;
        }
        catch
        {
            storage.Delete(stored.Key);
            throw;
        }
    }
}
