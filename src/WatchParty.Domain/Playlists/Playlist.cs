using WatchParty.Domain.Common;
using WatchParty.Domain.Users;

namespace WatchParty.Domain.Playlists;

public sealed class Playlist : Entity
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    private readonly List<PlaylistItem> _items = [];

    private Playlist() { }

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid OwnerId { get; private set; }
    public User Owner { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public IReadOnlyCollection<PlaylistItem> Items => _items;

    public static Playlist Create(string name, string? description, Guid ownerId, DateTime now)
    {
        var playlist = new Playlist { OwnerId = ownerId, CreatedAt = now };
        playlist.Rename(name, description);
        return playlist;
    }

    public void Rename(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("O nome da playlist é obrigatório.");

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    /// <summary>Adds a video at the end. Requires <see cref="Items"/> to be loaded.</summary>
    public Result<PlaylistItem> AddVideo(Guid videoId, DateTime now)
    {
        if (_items.Any(i => i.VideoId == videoId)) return PlaylistErrors.AlreadyContainsVideo;

        var position = _items.Count == 0 ? 0 : _items.Max(i => i.Position) + 1;
        var item = PlaylistItem.Create(Id, videoId, position, now);
        _items.Add(item);
        return item;
    }

    /// <summary>Removes a video. Requires <see cref="Items"/> to be loaded.</summary>
    public Result<PlaylistItem> RemoveVideo(Guid videoId)
    {
        var item = _items.FirstOrDefault(i => i.VideoId == videoId);
        if (item is null) return PlaylistErrors.VideoNotInPlaylist;

        _items.Remove(item);
        return item;
    }

    public bool IsOwnedBy(Guid userId) => OwnerId == userId;
}
