using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;

namespace WatchParty.Application.Features.Playlists;

public sealed record GetPlaylistsQuery : IQuery<IReadOnlyList<PlaylistSummaryDto>>;

internal sealed class GetPlaylistsQueryHandler(IAppDbContext db)
    : IQueryHandler<GetPlaylistsQuery, IReadOnlyList<PlaylistSummaryDto>>
{
    public async Task<IReadOnlyList<PlaylistSummaryDto>> Handle(GetPlaylistsQuery query, CancellationToken cancellationToken) =>
        await db.Playlists.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PlaylistSummaryDto(
                p.Id,
                p.Name,
                p.Description,
                p.OwnerId,
                p.Owner.DisplayName,
                p.Items.Count,
                p.Items.Where(i => i.Video.ThumbnailKey != null)
                    .OrderBy(i => i.Position)
                    .Select(i => (Guid?)i.VideoId)
                    .FirstOrDefault(),
                p.CreatedAt))
            .ToListAsync(cancellationToken);
}
