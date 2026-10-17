using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Domain.Common;
using WatchParty.Domain.Rooms;

namespace WatchParty.Application.Features.Rooms;

public sealed record DeleteRoomCommand(string Code, Guid UserId) : ICommand<Result>;

internal sealed class DeleteRoomCommandHandler(IAppDbContext db) : ICommandHandler<DeleteRoomCommand, Result>
{
    public async Task<Result> Handle(DeleteRoomCommand command, CancellationToken cancellationToken)
    {
        var code = RoomCode.Normalize(command.Code);
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Code == code, cancellationToken);
        if (room is null) return RoomErrors.NotFound;
        if (!room.IsHostedBy(command.UserId)) return RoomErrors.NotHost;

        db.Rooms.Remove(room);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
