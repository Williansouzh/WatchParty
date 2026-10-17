using WatchParty.Domain.Rooms;

namespace WatchParty.Tests.Domain;

public class RoomTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 20, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Position_advances_only_while_playing()
    {
        var room = Room.Create("abc123", "Sala", Guid.NewGuid(), Guid.NewGuid(), T0);

        room.Apply(PlaybackAction.Play, 10, T0);
        Assert.Equal(15, room.GetPositionAt(T0.AddSeconds(5)), precision: 3);

        room.Apply(PlaybackAction.Pause, 15, T0.AddSeconds(5));
        Assert.Equal(15, room.GetPositionAt(T0.AddMinutes(10)), precision: 3);
    }

    [Fact]
    public void Seek_keeps_playing_state()
    {
        var room = Room.Create("abc123", "Sala", Guid.NewGuid(), Guid.NewGuid(), T0);
        room.Apply(PlaybackAction.Play, 0, T0);

        room.Apply(PlaybackAction.Seek, 3600, T0.AddSeconds(1));

        Assert.True(room.IsPlaying);
        Assert.Equal(3602, room.GetPositionAt(T0.AddSeconds(3)), precision: 3);
    }

    [Fact]
    public void Playback_requires_a_video()
    {
        var room = Room.Create("abc123", "Sala", Guid.NewGuid(), videoId: null, T0);

        var result = room.Apply(PlaybackAction.Play, 0, T0);

        Assert.Equal(RoomErrors.NoVideo, result.Error);
    }

    [Fact]
    public void Changing_video_rewinds_and_pauses()
    {
        var room = Room.Create("abc123", "Sala", Guid.NewGuid(), Guid.NewGuid(), T0);
        room.Apply(PlaybackAction.Play, 500, T0);
        var newVideo = Guid.NewGuid();

        room.ChangeVideo(newVideo, T0.AddSeconds(1));

        Assert.Equal(newVideo, room.CurrentVideoId);
        Assert.False(room.IsPlaying);
        Assert.Equal(0, room.GetPositionAt(T0.AddSeconds(30)));
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    public void Invalid_positions_are_clamped(double position, double expected)
    {
        var room = Room.Create("abc123", "Sala", Guid.NewGuid(), Guid.NewGuid(), T0);

        room.Apply(PlaybackAction.Pause, position, T0);

        Assert.Equal(expected, room.PositionSeconds);
    }

    [Fact]
    public void Codes_are_normalized_and_readable()
    {
        var code = RoomCode.Generate();

        Assert.Equal(RoomCode.Length, code.Length);
        Assert.DoesNotContain(code, c => c is '0' or 'O' or '1' or 'I');
        Assert.Equal("K7P2QX", Room.Create(" k7p2qx ", "Sala", Guid.NewGuid(), null, T0).Code);
    }
}
