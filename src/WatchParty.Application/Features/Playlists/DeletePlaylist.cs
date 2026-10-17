using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Domain.Common;
using WatchParty.Domain.Playlists;

namespace WatchParty.Application.Features.Playlists;

public sealed record DeletePlaylistCommand(Guid PlaylistId, Guid UserId) : ICommand<Result>;

internal sealed class DeletePlaylistCommandHandler(IAppDbContext db) : ICommandHandler<DeletePlaylistCommand, Result>
{
    public async Task<Result> Handle(DeletePlaylistCommand command, CancellationToken cancellationToken)
    {
        var playlist = await db.Playlists.FirstOrDefaultAsync(p => p.Id == command.PlaylistId, cancellationToken);
        if (playlist is null) return PlaylistErrors.NotFound;
        if (!playlist.IsOwnedBy(command.UserId)) return PlaylistErrors.NotOwner;

        db.Playlists.Remove(playlist);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
