using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;
using WatchParty.Domain.Common;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Videos;

/// <summary>Deletes a video. Returns the codes of rooms that were playing it, so they can be told.</summary>
public sealed record DeleteVideoCommand(Guid VideoId, Guid UserId) : ICommand<Result<IReadOnlyList<string>>>;

internal sealed class DeleteVideoCommandHandler(IAppDbContext db, IVideoStorage storage)
    : ICommandHandler<DeleteVideoCommand, Result<IReadOnlyList<string>>>
{
    public async Task<Result<IReadOnlyList<string>>> Handle(DeleteVideoCommand command, CancellationToken cancellationToken)
    {
        var video = await db.Videos.FirstOrDefaultAsync(v => v.Id == command.VideoId, cancellationToken);
        if (video is null) return VideoErrors.NotFound;
        if (!video.IsOwnedBy(command.UserId)) return VideoErrors.NotOwner;

        var rooms = await db.Rooms.Where(r => r.CurrentVideoId == video.Id).Select(r => r.Code).ToListAsync(cancellationToken);
        var subtitleKeys = await db.Subtitles.Where(s => s.VideoId == video.Id).Select(s => s.FileKey).ToListAsync(cancellationToken);

        db.Videos.Remove(video);
        await db.SaveChangesAsync(cancellationToken);

        storage.Delete(video.FileKey);
        if (video.ThumbnailKey is not null) storage.Delete(video.ThumbnailKey);
        if (video.HlsKey is not null) storage.DeleteFolder(video.HlsKey);
        foreach (var key in subtitleKeys) storage.Delete(key);

        return rooms;
    }
}
