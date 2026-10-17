using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Domain.Common;
using WatchParty.Domain.Playlists;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Playlists;

// Playlists are collaborative: any friend can add or remove videos, only the owner renames or deletes.

public sealed record AddVideoToPlaylistCommand(Guid PlaylistId, Guid VideoId, Guid UserId) : ICommand<Result>;

public sealed record RemoveVideoFromPlaylistCommand(Guid PlaylistId, Guid VideoId, Guid UserId) : ICommand<Result>;

internal sealed class AddVideoToPlaylistCommandHandler(IAppDbContext db, TimeProvider time)
    : ICommandHandler<AddVideoToPlaylistCommand, Result>
{
    public async Task<Result> Handle(AddVideoToPlaylistCommand command, CancellationToken cancellationToken)
    {
        var playlist = await db.Playlists.Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == command.PlaylistId, cancellationToken);
        if (playlist is null) return PlaylistErrors.NotFound;
        if (!await db.Videos.AnyAsync(v => v.Id == command.VideoId, cancellationToken)) return VideoErrors.NotFound;

        var result = playlist.AddVideo(command.VideoId, time.GetUtcNow().UtcDateTime);
        if (result.IsFailure) return result.Error;

        db.PlaylistItems.Add(result.Value);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

internal sealed class RemoveVideoFromPlaylistCommandHandler(IAppDbContext db)
    : ICommandHandler<RemoveVideoFromPlaylistCommand, Result>
{
    public async Task<Result> Handle(RemoveVideoFromPlaylistCommand command, CancellationToken cancellationToken)
    {
        var playlist = await db.Playlists.Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == command.PlaylistId, cancellationToken);
        if (playlist is null) return PlaylistErrors.NotFound;

        var result = playlist.RemoveVideo(command.VideoId);
        if (result.IsFailure) return result.Error;

        db.PlaylistItems.Remove(result.Value);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
