using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Domain.Common;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Users;

namespace WatchParty.Application.Features.Chat;

public sealed record SendChatMessageCommand(string Code, Guid UserId, string Content) : ICommand<Result<ChatMessageDto>>;

internal sealed class SendChatMessageCommandValidator : AbstractValidator<SendChatMessageCommand>
{
    public SendChatMessageCommandValidator()
    {
        RuleFor(c => c.Content)
            .Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("A mensagem está vazia.")
            .MaximumLength(ChatMessage.ContentMaxLength).WithMessage("A mensagem é muito longa.");
    }
}

internal sealed class SendChatMessageCommandHandler(IAppDbContext db, TimeProvider time)
    : ICommandHandler<SendChatMessageCommand, Result<ChatMessageDto>>
{
    public async Task<Result<ChatMessageDto>> Handle(SendChatMessageCommand command, CancellationToken cancellationToken)
    {
        var code = RoomCode.Normalize(command.Code);
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Code == code, cancellationToken);
        if (room is null) return RoomErrors.NotFound;

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == command.UserId, cancellationToken);
        if (user is null) return UserErrors.NotFound;

        var now = time.GetUtcNow().UtcDateTime;
        var message = ChatMessage.Create(room.Id, user.Id, command.Content, now);
        room.Touch(now);
        db.ChatMessages.Add(message);
        await db.SaveChangesAsync(cancellationToken);

        return new ChatMessageDto(message.Id, user.Id, user.DisplayName, user.AvatarColor, message.Content, message.SentAt);
    }
}
