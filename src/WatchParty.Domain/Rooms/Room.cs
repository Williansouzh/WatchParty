using WatchParty.Domain.Common;
using WatchParty.Domain.Users;
using WatchParty.Domain.Videos;

namespace WatchParty.Domain.Rooms;

/// <summary>
/// A watch party. Holds the authoritative playback state: the position at <see cref="StateUpdatedAt"/>
/// and whether the video was playing from then on.
/// </summary>
public sealed class Room : Entity
{
    public const int NameMaxLength = 80;

    private Room() { }

    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public Guid HostId { get; private set; }
    public User Host { get; private set; } = null!;
    public Guid? CurrentVideoId { get; private set; }
    public Video? CurrentVideo { get; private set; }
    public bool IsPlaying { get; private set; }
    public double PositionSeconds { get; private set; }
    public DateTime StateUpdatedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public static Room Create(string code, string name, Guid hostId, Guid? videoId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("O nome da sala é obrigatório.");

        return new Room
        {
            Code = RoomCode.Normalize(code),
            Name = name.Trim(),
            HostId = hostId,
            CurrentVideoId = videoId,
            StateUpdatedAt = now,
            CreatedAt = now
        };
    }

    public double GetPositionAt(DateTime now) =>
        IsPlaying ? PositionSeconds + Math.Max(0, (now - StateUpdatedAt).TotalSeconds) : PositionSeconds;

    public Result Apply(PlaybackAction action, double positionSeconds, DateTime now)
    {
        if (CurrentVideoId is null) return RoomErrors.NoVideo;

        var playing = action switch
        {
            PlaybackAction.Play => true,
            PlaybackAction.Pause => false,
            _ => IsPlaying
        };
        SetState(playing, positionSeconds, now);
        return Result.Success();
    }

    public void ChangeVideo(Guid videoId, DateTime now)
    {
        CurrentVideoId = videoId;
        SetState(false, 0, now);
    }

    public bool IsHostedBy(Guid userId) => HostId == userId;

    private void SetState(bool isPlaying, double positionSeconds, DateTime now)
    {
        IsPlaying = isPlaying;
        PositionSeconds = double.IsFinite(positionSeconds) ? Math.Max(0, positionSeconds) : 0;
        StateUpdatedAt = now;
    }
}
