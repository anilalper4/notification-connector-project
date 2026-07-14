using Connector.Adapters;
using Connector.Contracts;

namespace Connector.Services;

public class ConnectorHostedService : BackgroundService
{
    private readonly IConnector _connector;
    private readonly WebhookSourceAdapter _webhookSourceAdapter;
    private readonly WebSocketSourceAdapter _webSocketSourceAdapter;
    private readonly BackendNotificationClient _backendNotificationClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConnectorHostedService> _logger;

    public ConnectorHostedService(
        IConnector connector,
        WebhookSourceAdapter webhookSourceAdapter,
        WebSocketSourceAdapter webSocketSourceAdapter,
        BackendNotificationClient backendNotificationClient,
        IConfiguration configuration,
        ILogger<ConnectorHostedService> logger
    )
    {
        _connector = connector;
        _webhookSourceAdapter = webhookSourceAdapter;
        _webSocketSourceAdapter = webSocketSourceAdapter;
        _backendNotificationClient = backendNotificationClient;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabledSources = GetEnabledSources();

        _connector.OnMessage += async notification =>
        {
            await _backendNotificationClient.SendAsync(notification, stoppingToken);
        };

        if (enabledSources.Contains("webhook"))
        {
            _connector.Register(_webhookSourceAdapter);
        }

        if (enabledSources.Contains("websocket"))
        {
            _connector.Register(_webSocketSourceAdapter);
        }

        await _connector.StartAsync(stoppingToken);

        _logger.LogInformation(
            "Connector started with sources: {Sources}",
            string.Join(", ", enabledSources)
        );

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _connector.StopAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    private HashSet<string> GetEnabledSources()
    {
        var rawSources = _configuration["CONNECTOR_SOURCES"]
            ?? "webhook,websocket";

        return rawSources
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(source => source.ToLowerInvariant())
            .ToHashSet();
    }
}