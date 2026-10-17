using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Domain.Common;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Features.Videos;

public sealed record GetVideoQuery(Guid VideoId, Guid UserId) : IQuery<Result<VideoDto>>;

internal sealed class GetVideoQueryHandler(IAppDbContext db) : IQueryHandler<GetVideoQuery, Result<VideoDto>>
{
    public async Task<Result<VideoDto>> Handle(GetVideoQuery query, CancellationToken cancellationToken)
    {
        var video = await db.Videos.AsNoTracking()
            .Where(v => v.Id == query.VideoId)
            .Select(VideoMappings.ToDto(db.Favorites, query.UserId))
            .FirstOrDefaultAsync(cancellationToken);

        return video is null ? VideoErrors.NotFound : video;
    }
}
