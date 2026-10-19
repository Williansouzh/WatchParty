using System.Collections.Concurrent;
using WatchParty.Application.Abstractions.Services;

namespace WatchParty.Infrastructure.Media;

internal sealed class VideoProgressTracker : IVideoProgressTracker
{
    private readonly ConcurrentDictionary<Guid, double> _progress = new();

    public void Report(Guid videoId, double progress) => _progress[videoId] = Math.Clamp(progress, 0, 1);

    public double? Get(Guid videoId) => _progress.TryGetValue(videoId, out var p) ? p : null;

    public void Clear(Guid videoId) => _progress.TryRemove(videoId, out _);
}
