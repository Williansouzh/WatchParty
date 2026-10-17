using WatchParty.Domain.Common;
using WatchParty.Domain.Users;

namespace WatchParty.Domain.Rooms;

public sealed class ChatMessage : Entity
{
    public const int ContentMaxLength = 1000;

    private ChatMessage() { }

    public Guid RoomId { get; private set; }
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public DateTime SentAt { get; private set; }

    public static ChatMessage Create(Guid roomId, Guid userId, string content, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(content)) throw new DomainException("A mensagem está vazia.");
        if (content.Length > ContentMaxLength) throw new DomainException("A mensagem é muito longa.");

        return new ChatMessage { RoomId = roomId, UserId = userId, Content = content.Trim(), SentAt = now };
    }
}
