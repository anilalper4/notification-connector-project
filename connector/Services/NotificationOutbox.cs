using System.Collections.Concurrent;
using Connector.Models;

namespace Connector.Services;

public class NotificationOutbox
{
    private readonly ConcurrentQueue<NotificationEnvelope> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);

    public int Count => _queue.Count;

    public void Enqueue(NotificationEnvelope notification)
    {
        _queue.Enqueue(notification);
        _signal.Release();
    }

    public bool TryDequeue(out NotificationEnvelope? notification)
    {
        return _queue.TryDequeue(out notification);
    }

    public async Task WaitForMessageAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken);
    }
}