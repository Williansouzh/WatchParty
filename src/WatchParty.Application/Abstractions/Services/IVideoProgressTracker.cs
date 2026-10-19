namespace WatchParty.Application.Abstractions.Services;

/// <summary>Conversion progress (0..1) of videos being processed. In memory; not worth persisting.</summary>
public interface IVideoProgressTracker
{
    void Report(Guid videoId, double progress);
    double? Get(Guid videoId);
    void Clear(Guid videoId);
}
