using System.Collections.Concurrent;
using Connector.Contracts;
using Connector.Models;
using Connector.Services;

namespace Connector.Core;

public class NotificationConnector : IConnector
{
    private readonly ConcurrentDictionary<string, ISourceAdapter> _adapters = new();
    private readonly NotificationNormalizer _normalizer;
    private readonly ILogger<NotificationConnector> _logger;

    public NotificationConnector(
        NotificationNormalizer normalizer,
        ILogger<NotificationConnector> logger
    )
    {
        _normalizer = normalizer;
        _logger = logger;
    }

    public event Func<NotificationEnvelope, Task>? OnMessage;

    public void Register(ISourceAdapter adapter)
    {
        if (_adapters.TryAdd(adapter.Name, adapter))
        {
            adapter.OnRawMessage += HandleRawMessageAsync;

            _logger.LogInformation(
                "Adapter registered: {AdapterName}",
                adapter.Name
            );
        }
    }

    public void Unregister(string adapterName)
    {
        if (_adapters.TryRemove(adapterName, out var adapter))
        {
            adapter.OnRawMessage -= HandleRawMessageAsync;

            _logger.LogInformation(
                "Adapter unregistered: {AdapterName}",
                adapterName
            );
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var adapter in _adapters.Values)
        {
            _logger.LogInformation(
                "Connecting adapter: {AdapterName}",
                adapter.Name
            );

            await adapter.ConnectAsync(cancellationToken);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var adapter in _adapters.Values)
        {
            _logger.LogInformation(
                "Disconnecting adapter: {AdapterName}",
                adapter.Name
            );

            await adapter.DisconnectAsync(cancellationToken);
        }
    }

    private async Task HandleRawMessageAsync(RawMessage rawMessage)
    {
        _logger.LogInformation(
            "Raw message received. Source: {Source}, Protocol: {Protocol}",
            rawMessage.Source,
            rawMessage.Protocol
        );

        var notification = _normalizer.Normalize(rawMessage);

        if (OnMessage is not null)
        {
            await OnMessage.Invoke(notification);
        }
    }
}