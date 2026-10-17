using WatchParty.Domain.Playlists;

namespace WatchParty.Tests.Domain;

public class PlaylistTests
{
    [Fact]
    public void Videos_are_appended_in_order_and_not_duplicated()
    {
        var playlist = Playlist.Create("Maratona", null, Guid.NewGuid(), DateTime.UtcNow);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var a = playlist.AddVideo(first, DateTime.UtcNow);
        var b = playlist.AddVideo(second, DateTime.UtcNow);
        var duplicate = playlist.AddVideo(first, DateTime.UtcNow);

        Assert.Equal(0, a.Value.Position);
        Assert.Equal(1, b.Value.Position);
        Assert.Equal(PlaylistErrors.AlreadyContainsVideo, duplicate.Error);
        Assert.Equal(2, playlist.Items.Count);
    }

    [Fact]
    public void Removing_a_missing_video_fails()
    {
        var playlist = Playlist.Create("Maratona", null, Guid.NewGuid(), DateTime.UtcNow);

        Assert.Equal(PlaylistErrors.VideoNotInPlaylist, playlist.RemoveVideo(Guid.NewGuid()).Error);
    }
}
