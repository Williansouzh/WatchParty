namespace WatchParty.Application.Abstractions.Services;

public interface IVideoProcessingQueue
{
    void Enqueue(Guid videoId);
}
