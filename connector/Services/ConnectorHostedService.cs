using Connector.Adapters;
using Connector.Contracts;

namespace Connector.Services;

public class ConnectorHostedService : BackgroundService
{
    private readonly IConnector _connector;
    private readonly WebhookSourceAdapter _webhookSourceAdapter;
    private readonly WebSocketSourceAdapter _webSocketSourceAdapter;
    private readonly RabbitMqSourceAdapter _rabbitMqSourceAdapter;
    private readonly RedisSourceAdapter _redisSourceAdapter;
    private readonly NotificationOutbox _outbox;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConnectorHostedService> _logger;

    public ConnectorHostedService(
        IConnector connector,
        WebhookSourceAdapter webhookSourceAdapter,
        WebSocketSourceAdapter webSocketSourceAdapter,
        RabbitMqSourceAdapter rabbitMqSourceAdapter,
        RedisSourceAdapter redisSourceAdapter,
        NotificationOutbox outbox,
        IConfiguration configuration,
        ILogger<ConnectorHostedService> logger
    )
    {
        _connector = connector;
        _webhookSourceAdapter = webhookSourceAdapter;
        _webSocketSourceAdapter = webSocketSourceAdapter;
        _rabbitMqSourceAdapter = rabbitMqSourceAdapter;
        _redisSourceAdapter = redisSourceAdapter;
        _outbox = outbox;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabledSources = GetEnabledSources();

        _connector.OnMessage += notification =>
        {
            _outbox.Enqueue(notification);

            _logger.LogInformation(
                "Notification added to outbox. Source: {Source}, Type: {Type}, PendingCount: {PendingCount}",
                notification.Source,
                notification.Type,
                _outbox.Count
            );

            return Task.CompletedTask;
        };

        if (enabledSources.Contains("webhook"))
        {
            _connector.Register(_webhookSourceAdapter);
        }

        if (enabledSources.Contains("websocket"))
        {
            _connector.Register(_webSocketSourceAdapter);
        }

        if (enabledSources.Contains("rabbitmq"))
        {
            _connector.Register(_rabbitMqSourceAdapter);
        }

        if (enabledSources.Contains("redis"))
        {
            _connector.Register(_redisSourceAdapter);
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
            ?? "webhook,websocket,rabbitmq,redis";

        return rawSources
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(source => source.ToLowerInvariant())
            .ToHashSet();
    }
}