using System.Threading.Channels;

namespace ThePlot.Grpc.Server.Services;

/// <summary>
/// In-memory fan-out bus for import status events. Subscribers register by blob name
/// and receive events via a dedicated channel. Thread-safe for concurrent publishers
/// and multiple streaming consumers.
/// </summary>
public sealed class ImportStatusEventBus
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, List<Channel<ImportStatusEvent>>> _subscribers = new();

    public ChannelReader<ImportStatusEvent> Subscribe(string blobName)
    {
        var channel = Channel.CreateUnbounded<ImportStatusEvent>(new UnboundedChannelOptions
        {
            SingleWriter = false,
            SingleReader = true
        });

        lock (_lock)
        {
            if (!_subscribers.TryGetValue(blobName, out var list))
            {
                list = [];
                _subscribers[blobName] = list;
            }
            list.Add(channel);
        }

        return channel.Reader;
    }

    public void Unsubscribe(string blobName, ChannelReader<ImportStatusEvent> reader)
    {
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(blobName, out var list)) return;
            list.RemoveAll(ch => ch.Reader == reader);
            if (list.Count == 0)
                _subscribers.Remove(blobName);
        }
    }

    public void Publish(string blobName, ImportStatusEvent evt)
    {
        List<Channel<ImportStatusEvent>>? snapshot;
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(blobName, out var list)) return;
            snapshot = [.. list];
        }

        foreach (var channel in snapshot)
        {
            channel.Writer.TryWrite(evt);
        }
    }

    public void Complete(string blobName)
    {
        List<Channel<ImportStatusEvent>>? snapshot;
        lock (_lock)
        {
            if (!_subscribers.Remove(blobName, out var list)) return;
            snapshot = list;
        }

        foreach (var channel in snapshot)
        {
            channel.Writer.TryComplete();
        }
    }
}
