using Connector.Models;

namespace Connector.Services;

public class BackendDeliveryWorker : BackgroundService
{
    private readonly NotificationOutbox _outbox;
    private readonly BackendNotificationClient _backendNotificationClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BackendDeliveryWorker> _logger;

    public BackendDeliveryWorker(
        NotificationOutbox outbox,
        BackendNotificationClient backendNotificationClient,
        IConfiguration configuration,
        ILogger<BackendDeliveryWorker> logger
    )
    {
        _outbox = outbox;
        _backendNotificationClient = backendNotificationClient;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retryDelaySeconds = GetRetryDelaySeconds();

        _logger.LogInformation(
            "Backend delivery worker started. Retry delay: {RetryDelaySeconds} seconds",
            retryDelaySeconds
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _outbox.WaitForMessageAsync(stoppingToken);

                while (_outbox.TryDequeue(out var notification) && notification is not null)
                {
                    var sent = await _backendNotificationClient.TrySendAsync(
                        notification,
                        stoppingToken
                    );

                    if (!sent)
                    {
                        _outbox.Enqueue(notification);

                        _logger.LogWarning(
                            "Notification requeued. Pending outbox count: {Count}",
                            _outbox.Count
                        );

                        await Task.Delay(
                            TimeSpan.FromSeconds(retryDelaySeconds),
                            stoppingToken
                        );

                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Unexpected backend delivery worker error."
                );

                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Backend delivery worker stopping. Trying to flush pending messages. Pending count: {Count}",
            _outbox.Count
        );

        var flushSeconds = GetShutdownFlushSeconds();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(flushSeconds);

        while (
            DateTimeOffset.UtcNow < deadline &&
            _outbox.TryDequeue(out NotificationEnvelope? notification) &&
            notification is not null &&
            !cancellationToken.IsCancellationRequested
        )
        {
            var sent = await _backendNotificationClient.TrySendAsync(
                notification,
                cancellationToken
            );

            if (!sent)
            {
                _outbox.Enqueue(notification);

                _logger.LogWarning(
                    "Could not flush notification before shutdown. Remaining count: {Count}",
                    _outbox.Count
                );

                break;
            }
        }

        await base.StopAsync(cancellationToken);
    }

    private int GetRetryDelaySeconds()
    {
        var rawValue = _configuration["BACKEND_RETRY_DELAY_SECONDS"];

        return int.TryParse(rawValue, out var value) && value > 0
            ? value
            : 3;
    }

    private int GetShutdownFlushSeconds()
    {
        var rawValue = _configuration["SHUTDOWN_FLUSH_SECONDS"];

        return int.TryParse(rawValue, out var value) && value > 0
            ? value
            : 10;
    }
}