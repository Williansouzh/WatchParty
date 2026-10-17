using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Features.Videos;
using WatchParty.Domain.Common;
using WatchParty.Domain.Playlists;

namespace WatchParty.Application.Features.Playlists;

public sealed record GetPlaylistQuery(Guid PlaylistId, Guid UserId) : IQuery<Result<PlaylistDto>>;

internal sealed class GetPlaylistQueryHandler(IAppDbContext db) : IQueryHandler<GetPlaylistQuery, Result<PlaylistDto>>
{
    public async Task<Result<PlaylistDto>> Handle(GetPlaylistQuery query, CancellationToken cancellationToken)
    {
        var playlist = await db.Playlists.AsNoTracking()
            .Where(p => p.Id == query.PlaylistId)
            .Select(p => new { p.Id, p.Name, p.Description, p.OwnerId, OwnerName = p.Owner.DisplayName, p.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);
        if (playlist is null) return PlaylistErrors.NotFound;

        var items = await db.PlaylistItems.AsNoTracking()
            .Where(i => i.PlaylistId == query.PlaylistId)
            .OrderBy(i => i.Position)
            .Select(i => new { i.Id, i.VideoId, i.AddedAt })
            .ToListAsync(cancellationToken);

        var videoIds = items.Select(i => i.VideoId).ToList();
        var videos = await db.Videos.AsNoTracking()
            .Where(v => videoIds.Contains(v.Id))
            .Select(VideoMappings.ToDto(db.Favorites, query.UserId))
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        return new PlaylistDto(
            playlist.Id,
            playlist.Name,
            playlist.Description,
            playlist.OwnerId,
            playlist.OwnerName,
            playlist.CreatedAt,
            items.Where(i => videos.ContainsKey(i.VideoId))
                .Select(i => new PlaylistItemDto(i.Id, i.AddedAt, videos[i.VideoId]))
                .ToList());
    }
}
