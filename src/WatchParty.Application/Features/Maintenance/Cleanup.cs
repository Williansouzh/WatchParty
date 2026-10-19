using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;

namespace WatchParty.Application.Features.Maintenance;

public sealed record CleanupResult(int RoomsDeleted, int MessagesDeleted);

/// <summary>Deletes idle rooms nobody is in and very old chat messages.</summary>
public sealed record CleanupCommand : ICommand<CleanupResult>;

internal sealed class CleanupCommandHandler(IAppDbContext db, IRoomPresence presence, IOptions<MaintenanceOptions> options, TimeProvider time)
    : ICommandHandler<CleanupCommand, CleanupResult>
{
    public async Task<CleanupResult> Handle(CleanupCommand command, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var settings = options.Value;
        var roomsDeleted = 0;
        var messagesDeleted = 0;

        if (settings.RoomInactiveDays > 0)
        {
            var limit = now.AddDays(-settings.RoomInactiveDays);
            var idle = await db.Rooms.Where(r => r.LastActivityAt < limit).ToListAsync(cancellationToken);
            foreach (var room in idle.Where(r => presence.CountUsers(r.Code) == 0))
            {
                db.Rooms.Remove(room);
                roomsDeleted++;
            }
        }

        if (settings.ChatRetentionDays > 0)
        {
            var limit = now.AddDays(-settings.ChatRetentionDays);
            var old = await db.ChatMessages.Where(m => m.SentAt < limit).ToListAsync(cancellationToken);
            db.ChatMessages.RemoveRange(old);
            messagesDeleted = old.Count;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new CleanupResult(roomsDeleted, messagesDeleted);
    }
}
