using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Features.Videos;
using WatchParty.Domain.Common;
using WatchParty.Domain.Playlists;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Rooms;

// Anyone can suggest videos for the queue; skipping/starting the next one follows the host-only rule.

public sealed record AddToQueueCommand(string Code, Guid UserId, Guid VideoId) : ICommand<Result<IReadOnlyList<QueueItemDto>>>;

public sealed record AddPlaylistToQueueCommand(string Code, Guid UserId, Guid PlaylistId) : ICommand<Result<IReadOnlyList<QueueItemDto>>>;

public sealed record RemoveFromQueueCommand(string Code, Guid UserId, Guid ItemId) : ICommand<Result<IReadOnlyList<QueueItemDto>>>;

/// <summary>
/// Starts the first queued video. With <paramref name="FinishedVideoId"/> (sent by players when the video ends) it only
/// advances if that video is still the current one, so many players reporting the end advance just once.
/// </summary>
public sealed record PlayNextCommand(string Code, Guid UserId, Guid? FinishedVideoId = null) : ICommand<Result<RoomVideoChangedDto?>>;

internal sealed class AddToQueueCommandHandler(IAppDbContext db, TimeProvider time)
    : ICommandHandler<AddToQueueCommand, Result<IReadOnlyList<QueueItemDto>>>
{
    public async Task<Result<IReadOnlyList<QueueItemDto>>> Handle(AddToQueueCommand command, CancellationToken cancellationToken)
    {
        var code = RoomCode.Normalize(command.Code);
        var roomId = await db.Rooms.Where(r => r.Code == code).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(cancellationToken);
        if (roomId is null) return RoomErrors.NotFound;

        var status = await db.Videos.Where(v => v.Id == command.VideoId).Select(v => (VideoStatus?)v.Status).FirstOrDefaultAsync(cancellationToken);
        if (status is null) return VideoErrors.NotFound;
        if (status != VideoStatus.Ready) return VideoErrors.NotReady;

        var position = await RoomLoader.NextQueuePositionAsync(db, roomId.Value, cancellationToken);
        db.RoomQueue.Add(RoomQueueItem.Create(roomId.Value, command.VideoId, position, command.UserId, time.GetUtcNow().UtcDateTime));
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await RoomLoader.LoadQueueAsync(db, roomId.Value, command.UserId, cancellationToken));
    }
}

internal sealed class AddPlaylistToQueueCommandHandler(IAppDbContext db, TimeProvider time)
    : ICommandHandler<AddPlaylistToQueueCommand, Result<IReadOnlyList<QueueItemDto>>>
{
    public async Task<Result<IReadOnlyList<QueueItemDto>>> Handle(AddPlaylistToQueueCommand command, CancellationToken cancellationToken)
    {
        var code = RoomCode.Normalize(command.Code);
        var roomId = await db.Rooms.Where(r => r.Code == code).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(cancellationToken);
        if (roomId is null) return RoomErrors.NotFound;
        if (!await db.Playlists.AnyAsync(p => p.Id == command.PlaylistId, cancellationToken)) return PlaylistErrors.NotFound;

        var videoIds = await db.PlaylistItems
            .Where(i => i.PlaylistId == command.PlaylistId && i.Video.Status == VideoStatus.Ready)
            .OrderBy(i => i.Position)
            .Select(i => i.VideoId)
            .ToListAsync(cancellationToken);

        var position = await RoomLoader.NextQueuePositionAsync(db, roomId.Value, cancellationToken);
        var now = time.GetUtcNow().UtcDateTime;
        foreach (var videoId in videoIds)
            db.RoomQueue.Add(RoomQueueItem.Create(roomId.Value, videoId, position++, command.UserId, now));
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await RoomLoader.LoadQueueAsync(db, roomId.Value, command.UserId, cancellationToken));
    }
}

internal sealed class RemoveFromQueueCommandHandler(IAppDbContext db)
    : ICommandHandler<RemoveFromQueueCommand, Result<IReadOnlyList<QueueItemDto>>>
{
    public async Task<Result<IReadOnlyList<QueueItemDto>>> Handle(RemoveFromQueueCommand command, CancellationToken cancellationToken)
    {
        var code = RoomCode.Normalize(command.Code);
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Code == code, cancellationToken);
        if (room is null) return RoomErrors.NotFound;

        var item = await db.RoomQueue.FirstOrDefaultAsync(q => q.Id == command.ItemId && q.RoomId == room.Id, cancellationToken);
        if (item is null) return RoomErrors.QueueItemNotFound;
        if (item.AddedById != command.UserId && !room.CanControl(command.UserId)) return RoomErrors.HostOnly;

        db.RoomQueue.Remove(item);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await RoomLoader.LoadQueueAsync(db, room.Id, command.UserId, cancellationToken));
    }
}

internal sealed class PlayNextCommandHandler(IAppDbContext db, TimeProvider time)
    : ICommandHandler<PlayNextCommand, Result<RoomVideoChangedDto?>>
{
    public async Task<Result<RoomVideoChangedDto?>> Handle(PlayNextCommand command, CancellationToken cancellationToken)
    {
        var code = RoomCode.Normalize(command.Code);
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Code == code, cancellationToken);
        if (room is null) return RoomErrors.NotFound;

        var automatic = command.FinishedVideoId is not null;
        if (automatic && room.CurrentVideoId != command.FinishedVideoId) return Result.Success<RoomVideoChangedDto?>(null);
        if (!automatic && !room.CanControl(command.UserId)) return RoomErrors.HostOnly;

        var next = await db.RoomQueue.Where(q => q.RoomId == room.Id).OrderBy(q => q.Position).FirstOrDefaultAsync(cancellationToken);
        if (next is null) return automatic ? Result.Success<RoomVideoChangedDto?>(null) : RoomErrors.QueueEmpty;

        db.RoomQueue.Remove(next);
        room.ChangeVideo(next.VideoId, time.GetUtcNow().UtcDateTime);
        // Keep the marathon going without someone having to press play.
        room.Apply(PlaybackAction.Play, 0, time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);

        var video = await GetVideoQueryHandler.LoadAsync(db, next.VideoId, command.UserId, cancellationToken);
        return new RoomVideoChangedDto(video!, PlaybackStateDto.From(room),
            await RoomLoader.LoadQueueAsync(db, room.Id, command.UserId, cancellationToken));
    }
}
