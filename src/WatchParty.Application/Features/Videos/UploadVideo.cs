using FluentValidation;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Domain.Common;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Videos;

public sealed record UploadVideoCommand(
    Guid UserId,
    string Title,
    string? Description,
    string FileName,
    Stream Content) : ICommand<Result<Guid>>;

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
    TimeProvider time) : ICommandHandler<UploadVideoCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(UploadVideoCommand command, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(command.FileName).ToLowerInvariant();
        var stored = await storage.SaveAsync(command.Content, extension, cancellationToken);

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
