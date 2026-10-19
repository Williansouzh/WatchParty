using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Features.Videos;

namespace WatchParty.Application.Features.Rooms;

internal static class RoomLoader
{
    public static async Task<IReadOnlyList<QueueItemDto>> LoadQueueAsync(IAppDbContext db, Guid roomId, Guid userId, CancellationToken ct)
    {
        var items = await db.RoomQueue.AsNoTracking()
            .Where(q => q.RoomId == roomId)
            .OrderBy(q => q.Position)
            .Select(q => new { q.Id, q.VideoId, q.AddedById, AddedByName = q.AddedBy.DisplayName })
            .ToListAsync(ct);
        if (items.Count == 0) return [];

        var ids = items.Select(i => i.VideoId).Distinct().ToList();
        var videos = await db.Videos.AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .Select(VideoMappings.ToDto(db.Favorites, userId))
            .ToDictionaryAsync(v => v.Id, ct);

        return items.Where(i => videos.ContainsKey(i.VideoId))
            .Select(i => new QueueItemDto(i.Id, videos[i.VideoId], i.AddedById, i.AddedByName))
            .ToList();
    }

    public static async Task<int> NextQueuePositionAsync(IAppDbContext db, Guid roomId, CancellationToken ct) =>
        (await db.RoomQueue.Where(q => q.RoomId == roomId).MaxAsync(q => (int?)q.Position, ct) ?? -1) + 1;
}
