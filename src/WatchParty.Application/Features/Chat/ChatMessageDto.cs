namespace WatchParty.Application.Features.Chat;

public sealed record ChatMessageDto(Guid Id, Guid UserId, string DisplayName, string AvatarColor, string Content, DateTime SentAt);
