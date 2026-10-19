using Microsoft.EntityFrameworkCore;
using WatchParty.Application.Abstractions.Data;
using WatchParty.Application.Abstractions.Messaging;
using WatchParty.Application.Abstractions.Services;

namespace WatchParty.Application.Features.Videos;

public enum LibraryFilter
{
    All,
    Favorites,
    MyUploads
}

public sealed record GetLibraryQuery(Guid UserId, string? Search = null, LibraryFilter Filter = LibraryFilter.All, int? Take = null)
    : IQuery<IReadOnlyList<VideoDto>>;

internal sealed class GetLibraryQueryHandler(IAppDbContext db, IVideoProgressTracker progress) : IQueryHandler<GetLibraryQuery, IReadOnlyList<VideoDto>>
{
    public async Task<IReadOnlyList<VideoDto>> Handle(GetLibraryQuery query, CancellationToken cancellationToken)
    {
        var videos = db.Videos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            videos = videos.Where(v => v.Title.ToLower().Contains(term) || (v.Description != null && v.Description.ToLower().Contains(term)));
        }

        videos = query.Filter switch
        {
            LibraryFilter.Favorites => videos.Where(v => db.Favorites.Any(f => f.UserId == query.UserId && f.VideoId == v.Id)),
            LibraryFilter.MyUploads => videos.Where(v => v.UploadedById == query.UserId),
            _ => videos
        };

        var ordered = videos.OrderByDescending(v => v.UploadedAt).AsQueryable();
        if (query.Take is > 0) ordered = ordered.Take(query.Take.Value);

        var result = await ordered.Select(VideoMappings.ToDto(db.Favorites, query.UserId)).ToListAsync(cancellationToken);
        return result.Select(v => v.WithProgress(progress)).ToList();
    }
}
