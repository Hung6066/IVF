using System.Threading.Channels;
using IVF.Application.Common.Interfaces;

namespace IVF.Infrastructure.Services;

/// <summary>
/// Singleton bounded channel for fire-and-forget WAF event ingestion.
/// Drops oldest events on overflow to prevent backpressure.
/// </summary>
public sealed class WafEventChannel
{
    private readonly Channel<WafEventData> _channel;

    public WafEventChannel()
    {
        _channel = Channel.CreateBounded<WafEventData>(new BoundedChannelOptions(10_000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ChannelWriter<WafEventData> Writer => _channel.Writer;
    public ChannelReader<WafEventData> Reader => _channel.Reader;
}
