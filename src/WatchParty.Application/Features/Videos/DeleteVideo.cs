using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Domain.Common;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Videos;

public sealed record DeleteVideoCommand(Guid VideoId, Guid UserId) : ICommand<Result>;

internal sealed class DeleteVideoCommandHandler(IAppDbContext db, IVideoStorage storage)
    : ICommandHandler<DeleteVideoCommand, Result>
{
    public async Task<Result> Handle(DeleteVideoCommand command, CancellationToken cancellationToken)
    {
        var video = await db.Videos.FirstOrDefaultAsync(v => v.Id == command.VideoId, cancellationToken);
        if (video is null) return VideoErrors.NotFound;
        if (!video.IsOwnedBy(command.UserId)) return VideoErrors.NotOwner;

        db.Videos.Remove(video);
        await db.SaveChangesAsync(cancellationToken);

        storage.Delete(video.FileKey);
        if (video.ThumbnailKey is not null) storage.Delete(video.ThumbnailKey);

        return Result.Success();
    }
}
