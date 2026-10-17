using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Features.Videos;
using WatchParty.Domain.Common;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Rooms;

// Everybody in the room can control playback: it is a party among friends.

public sealed record ControlPlaybackCommand(string Code, Guid UserId, PlaybackAction Action, double PositionSeconds)
    : ICommand<Result<PlaybackStateDto>>;

public sealed record ChangeRoomVideoCommand(string Code, Guid UserId, Guid VideoId) : ICommand<Result<RoomVideoChangedDto>>;

public sealed record GetPlaybackQuery(string Code) : IQuery<Result<PlaybackStateDto>>;

internal sealed class ControlPlaybackCommandHandler(IAppDbContext db, TimeProvider time)
    : ICommandHandler<ControlPlaybackCommand, Result<PlaybackStateDto>>
{
    public async Task<Result<PlaybackStateDto>> Handle(ControlPlaybackCommand command, CancellationToken cancellationToken)
    {
        var code = RoomCode.Normalize(command.Code);
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Code == code, cancellationToken);
        if (room is null) return RoomErrors.NotFound;

        var result = room.Apply(command.Action, command.PositionSeconds, time.GetUtcNow().UtcDateTime);
        if (result.IsFailure) return result.Error;

        await db.SaveChangesAsync(cancellationToken);
        return PlaybackStateDto.From(room);
    }
}

internal sealed class ChangeRoomVideoCommandHandler(IAppDbContext db, TimeProvider time)
    : ICommandHandler<ChangeRoomVideoCommand, Result<RoomVideoChangedDto>>
{
    public async Task<Result<RoomVideoChangedDto>> Handle(ChangeRoomVideoCommand command, CancellationToken cancellationToken)
    {
        var code = RoomCode.Normalize(command.Code);
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Code == code, cancellationToken);
        if (room is null) return RoomErrors.NotFound;

        var video = await db.Videos.AsNoTracking()
            .Where(v => v.Id == command.VideoId)
            .Select(VideoMappings.ToDto(db.Favorites, command.UserId))
            .FirstOrDefaultAsync(cancellationToken);
        if (video is null) return VideoErrors.NotFound;
        if (video.Status != VideoStatus.Ready) return VideoErrors.NotReady;

        room.ChangeVideo(video.Id, time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);

        return new RoomVideoChangedDto(video, PlaybackStateDto.From(room));
    }
}

internal sealed class GetPlaybackQueryHandler(IAppDbContext db) : IQueryHandler<GetPlaybackQuery, Result<PlaybackStateDto>>
{
    public async Task<Result<PlaybackStateDto>> Handle(GetPlaybackQuery query, CancellationToken cancellationToken)
    {
        var code = RoomCode.Normalize(query.Code);
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Code == code, cancellationToken);
        return room is null ? RoomErrors.NotFound : PlaybackStateDto.From(room);
    }
}
