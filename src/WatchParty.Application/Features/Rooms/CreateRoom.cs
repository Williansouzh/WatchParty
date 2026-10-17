using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Domain.Common;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Rooms;

/// <summary>Creates a room and returns its invite code.</summary>
public sealed record CreateRoomCommand(Guid UserId, string Name, Guid? VideoId) : ICommand<Result<string>>;

internal sealed class CreateRoomCommandValidator : AbstractValidator<CreateRoomCommand>
{
    public CreateRoomCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Dê um nome à sala.").MaximumLength(Room.NameMaxLength);
    }
}

internal sealed class CreateRoomCommandHandler(IAppDbContext db, TimeProvider time)
    : ICommandHandler<CreateRoomCommand, Result<string>>
{
    public async Task<Result<string>> Handle(CreateRoomCommand command, CancellationToken cancellationToken)
    {
        if (command.VideoId is { } videoId)
        {
            var status = await db.Videos.Where(v => v.Id == videoId).Select(v => (VideoStatus?)v.Status)
                .FirstOrDefaultAsync(cancellationToken);
            if (status is null) return VideoErrors.NotFound;
            if (status != VideoStatus.Ready) return VideoErrors.NotReady;
        }

        string code;
        do code = RoomCode.Generate();
        while (await db.Rooms.AnyAsync(r => r.Code == code, cancellationToken));

        var room = Room.Create(code, command.Name, command.UserId, command.VideoId, time.GetUtcNow().UtcDateTime);
        db.Rooms.Add(room);
        await db.SaveChangesAsync(cancellationToken);

        return room.Code;
    }
}
