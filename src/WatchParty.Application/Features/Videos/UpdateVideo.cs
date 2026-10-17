using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Domain.Common;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Videos;

public sealed record UpdateVideoCommand(Guid VideoId, Guid UserId, string Title, string? Description) : ICommand<Result>;

internal sealed class UpdateVideoCommandValidator : AbstractValidator<UpdateVideoCommand>
{
    public UpdateVideoCommandValidator()
    {
        RuleFor(c => c.Title).NotEmpty().WithMessage("Informe um título.").MaximumLength(Video.TitleMaxLength);
        RuleFor(c => c.Description).MaximumLength(Video.DescriptionMaxLength);
    }
}

internal sealed class UpdateVideoCommandHandler(IAppDbContext db) : ICommandHandler<UpdateVideoCommand, Result>
{
    public async Task<Result> Handle(UpdateVideoCommand command, CancellationToken cancellationToken)
    {
        var video = await db.Videos.FirstOrDefaultAsync(v => v.Id == command.VideoId, cancellationToken);
        if (video is null) return VideoErrors.NotFound;
        if (!video.IsOwnedBy(command.UserId)) return VideoErrors.NotOwner;

        video.UpdateDetails(command.Title, command.Description);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
