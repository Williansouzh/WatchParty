using FluentValidation;
using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Domain.Common;
using WatchParty.Domain.Playlists;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Rooms;

/// <summary>
/// Creates a room and returns its invite code. With a playlist, its first ready video starts and the rest
/// goes to the queue ("maratona").
/// </summary>
public sealed record CreateRoomCommand(Guid UserId, string Name, Guid? VideoId, Guid? PlaylistId = null) : ICommand<Result<string>>;

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
        var now = time.GetUtcNow().UtcDateTime;
        var videoId = command.VideoId;
        List<Guid> queue = [];

        if (command.PlaylistId is { } playlistId)
        {
            if (!await db.Playlists.AnyAsync(p => p.Id == playlistId, cancellationToken)) return PlaylistErrors.NotFound;

            var ready = await db.PlaylistItems
                .Where(i => i.PlaylistId == playlistId && i.Video.Status == VideoStatus.Ready)
                .OrderBy(i => i.Position)
                .Select(i => i.VideoId)
                .ToListAsync(cancellationToken);
            if (ready.Count == 0) return VideoErrors.NotReady;

            videoId ??= ready[0];
            queue = ready.Where(id => id != videoId).ToList();
        }

        if (videoId is { } id)
        {
            var status = await db.Videos.Where(v => v.Id == id).Select(v => (VideoStatus?)v.Status).FirstOrDefaultAsync(cancellationToken);
            if (status is null) return VideoErrors.NotFound;
            if (status != VideoStatus.Ready) return VideoErrors.NotReady;
        }

        string code;
        do code = RoomCode.Generate();
        while (await db.Rooms.AnyAsync(r => r.Code == code, cancellationToken));

        var room = Room.Create(code, command.Name, command.UserId, videoId, now);
        db.Rooms.Add(room);
        for (var i = 0; i < queue.Count; i++)
            db.RoomQueue.Add(RoomQueueItem.Create(room.Id, queue[i], i, command.UserId, now));
        await db.SaveChangesAsync(cancellationToken);

        return room.Code;
    }
}
