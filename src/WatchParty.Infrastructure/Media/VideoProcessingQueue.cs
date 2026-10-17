using System.Threading.Channels;
using WatchParty.Application.Abstractions.Services;

namespace WatchParty.Infrastructure.Media;

internal sealed class VideoProcessingQueue : IVideoProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelReader<Guid> Reader => _channel.Reader;

    public void Enqueue(Guid videoId) => _channel.Writer.TryWrite(videoId);
}
