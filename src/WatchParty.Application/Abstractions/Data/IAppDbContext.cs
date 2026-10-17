using Microsoft.EntityFrameworkCore;
using WatchParty.Domain.Playlists;
using WatchParty.Domain.Rooms;
using WatchParty.Domain.Users;
using WatchParty.Domain.Videos;

namespace WatchParty.Application.Abstractions.Data;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Video> Videos { get; }
    DbSet<Favorite> Favorites { get; }
    DbSet<Playlist> Playlists { get; }
    DbSet<PlaylistItem> PlaylistItems { get; }
    DbSet<Room> Rooms { get; }
    DbSet<ChatMessage> ChatMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
