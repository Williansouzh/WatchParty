using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;

namespace WatchParty.Application.Features.Rooms;

public sealed record GetRoomsQuery : IQuery<IReadOnlyList<RoomSummaryDto>>;

internal sealed class GetRoomsQueryHandler(IAppDbContext db, IRoomPresence presence)
    : IQueryHandler<GetRoomsQuery, IReadOnlyList<RoomSummaryDto>>
{
    public async Task<IReadOnlyList<RoomSummaryDto>> Handle(GetRoomsQuery query, CancellationToken cancellationToken)
    {
        var rooms = await db.Rooms.AsNoTracking()
            .Select(r => new
            {
                r.Code,
                r.Name,
                HostName = r.Host.DisplayName,
                r.CurrentVideoId,
                VideoTitle = r.CurrentVideo != null ? r.CurrentVideo.Title : null,
                HasThumbnail = r.CurrentVideo != null && r.CurrentVideo.ThumbnailKey != null,
                r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Rooms with people in them first.
        return rooms
            .Select(r => new RoomSummaryDto(r.Code, r.Name, r.HostName, r.CurrentVideoId, r.VideoTitle, r.HasThumbnail,
                presence.CountUsers(r.Code), r.CreatedAt))
            .OrderByDescending(r => r.OnlineCount)
            .ThenByDescending(r => r.CreatedAt)
            .ToList();
    }
}
