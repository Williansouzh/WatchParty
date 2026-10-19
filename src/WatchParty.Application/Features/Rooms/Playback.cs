using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Features.Videos;
using WatchParty.Domain.Common;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Rooms;

// Everybody controls playback unless the host enabled "only the host controls".

/// <param name="IsSystem">Automatic actions (e.g. waiting for someone who is loading) skip the host-only rule.</param>
public sealed record ControlPlaybackCommand(string Code, Guid UserId, PlaybackAction Action, double PositionSeconds, bool IsSystem = false)
    : ICommand<Result<PlaybackStateDto>>;

public sealed record ChangeRoomVideoCommand(string Code, Guid UserId, Guid VideoId) : ICommand<Result<RoomVideoChangedDto>>;

public sealed record GetPlaybackQuery(string Code) : IQuery<Result<PlaybackStateDto>>;

public sealed record SetHostOnlyControlCommand(string Code, Guid UserId, bool HostOnly) : ICommand<Result>;

internal sealed class ControlPlaybackCommandHandler(IAppDbContext db, TimeProvider time)
    : ICommandHandler<ControlPlaybackCommand, Result<PlaybackStateDto>>
{
    public async Task<Result<PlaybackStateDto>> Handle(ControlPlaybackCommand command, CancellationToken cancellationToken)
    {
        var code = RoomCode.Normalize(command.Code);
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Code == code, cancellationToken);
        if (room is null) return RoomErrors.NotFound;
        if (!command.IsSystem && !room.CanControl(command.UserId)) return RoomErrors.HostOnly;

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
        if (!room.CanControl(command.UserId)) return RoomErrors.HostOnly;

        var video = await GetVideoQueryHandler.LoadAsync(db, command.VideoId, command.UserId, cancellationToken);
        if (video is null) return VideoErrors.NotFound;
        if (video.Status != VideoStatus.Ready) return VideoErrors.NotReady;

        room.ChangeVideo(video.Id, time.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);

        return new RoomVideoChangedDto(video, PlaybackStateDto.From(room),
            await RoomLoader.LoadQueueAsync(db, room.Id, command.UserId, cancellationToken));
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

internal sealed class SetHostOnlyControlCommandHandler(IAppDbContext db) : ICommandHandler<SetHostOnlyControlCommand, Result>
{
    public async Task<Result> Handle(SetHostOnlyControlCommand command, CancellationToken cancellationToken)
    {
        var code = RoomCode.Normalize(command.Code);
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Code == code, cancellationToken);
        if (room is null) return RoomErrors.NotFound;

        var result = room.SetHostOnlyControl(command.UserId, command.HostOnly);
        if (result.IsFailure) return result;

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
