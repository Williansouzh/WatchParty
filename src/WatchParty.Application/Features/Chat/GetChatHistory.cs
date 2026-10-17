using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Domain.Common;
using WatchParty.Domain.Rooms;

namespace WatchParty.Application.Features.Chat;

/// <summary>Latest messages of a room, oldest first.</summary>
public sealed record GetChatHistoryQuery(string Code, int Take = 100) : IQuery<Result<IReadOnlyList<ChatMessageDto>>>;

internal sealed class GetChatHistoryQueryHandler(IAppDbContext db)
    : IQueryHandler<GetChatHistoryQuery, Result<IReadOnlyList<ChatMessageDto>>>
{
    public async Task<Result<IReadOnlyList<ChatMessageDto>>> Handle(GetChatHistoryQuery query, CancellationToken cancellationToken)
    {
        var code = RoomCode.Normalize(query.Code);
        var roomId = await db.Rooms.Where(r => r.Code == code).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(cancellationToken);
        if (roomId is null) return RoomErrors.NotFound;

        var messages = await db.ChatMessages.AsNoTracking()
            .Where(m => m.RoomId == roomId)
            .OrderByDescending(m => m.SentAt)
            .Take(Math.Clamp(query.Take, 1, 500))
            .Select(m => new ChatMessageDto(m.Id, m.UserId, m.User.DisplayName, m.User.AvatarColor, m.Content, m.SentAt))
            .ToListAsync(cancellationToken);

        messages.Reverse();
        return messages;
    }
}
