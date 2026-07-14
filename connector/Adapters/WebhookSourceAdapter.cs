using Connector.Contracts;
using Connector.Models;

namespace Connector.Adapters;

public class WebhookSourceAdapter : ISourceAdapter
{
    private readonly ILogger<WebhookSourceAdapter> _logger;

    public WebhookSourceAdapter(ILogger<WebhookSourceAdapter> logger)
    {
        _logger = logger;
    }

    public string Name => "webhook";

    public event Func<RawMessage, Task>? OnRawMessage;

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Webhook adapter is ready to receive HTTP messages.");
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Webhook adapter disconnected.");
        return Task.CompletedTask;
    }

    public async Task ReceiveAsync(string payload)
    {
        if (OnRawMessage is null)
        {
            _logger.LogWarning("Webhook raw message ignored because no handler is registered.");
            return;
        }

        var rawMessage = new RawMessage(
            Source: "webhook",
            Protocol: "webhook",
            Payload: payload,
            ReceivedAt: DateTimeOffset.UtcNow
        );

        await OnRawMessage.Invoke(rawMessage);
    }
}