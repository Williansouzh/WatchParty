using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Domain.Common;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Videos;

/// <summary>Likes or unlikes a video. Returns whether it is now liked.</summary>
public sealed record ToggleFavoriteCommand(Guid VideoId, Guid UserId) : ICommand<Result<bool>>;

internal sealed class ToggleFavoriteCommandHandler(IAppDbContext db, TimeProvider time)
    : ICommandHandler<ToggleFavoriteCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ToggleFavoriteCommand command, CancellationToken cancellationToken)
    {
        if (!await db.Videos.AnyAsync(v => v.Id == command.VideoId, cancellationToken)) return VideoErrors.NotFound;

        var favorite = await db.Favorites.FirstOrDefaultAsync(
            f => f.UserId == command.UserId && f.VideoId == command.VideoId, cancellationToken);

        if (favorite is null)
            db.Favorites.Add(Favorite.Create(command.UserId, command.VideoId, time.GetUtcNow().UtcDateTime));
        else
            db.Favorites.Remove(favorite);

        await db.SaveChangesAsync(cancellationToken);
        return favorite is null;
    }
}
