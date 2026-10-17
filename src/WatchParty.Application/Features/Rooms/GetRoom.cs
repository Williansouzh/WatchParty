using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Application.Features.Videos;
using WatchParty.Domain.Common;
using WatchParty.Domain.Rooms;

namespace WatchParty.Application.Features.Rooms;

public sealed record GetRoomQuery(string Code, Guid UserId) : IQuery<Result<RoomDto>>;

internal sealed class GetRoomQueryHandler(IAppDbContext db, IRoomPresence presence)
    : IQueryHandler<GetRoomQuery, Result<RoomDto>>
{
    public async Task<Result<RoomDto>> Handle(GetRoomQuery query, CancellationToken cancellationToken)
    {
        var code = RoomCode.Normalize(query.Code);
        var room = await db.Rooms.AsNoTracking().Include(r => r.Host)
            .FirstOrDefaultAsync(r => r.Code == code, cancellationToken);
        if (room is null) return RoomErrors.NotFound;

        VideoDto? video = null;
        if (room.CurrentVideoId is { } videoId)
        {
            video = await db.Videos.AsNoTracking()
                .Where(v => v.Id == videoId)
                .Select(VideoMappings.ToDto(db.Favorites, query.UserId))
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new RoomDto(
            room.Id,
            room.Code,
            room.Name,
            room.HostId,
            room.Host.DisplayName,
            video,
            PlaybackStateDto.From(room),
            presence.CountUsers(room.Code),
            room.CreatedAt);
    }
}
