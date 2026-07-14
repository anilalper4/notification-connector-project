using System.Net.WebSockets;
using System.Text;
using Connector.Contracts;
using Connector.Models;

namespace Connector.Adapters;

public class WebSocketSourceAdapter : ISourceAdapter
{
    private readonly ILogger<WebSocketSourceAdapter> _logger;
    private readonly string _webSocketUrl;
    private ClientWebSocket? _clientWebSocket;
    private Task? _listeningTask;

    public WebSocketSourceAdapter(
        IConfiguration configuration,
        ILogger<WebSocketSourceAdapter> logger
    )
    {
        _logger = logger;

        _webSocketUrl = configuration["WEBSOCKET_SOURCE_URL"]
            ?? "ws://localhost:7001/ws";
    }

    public string Name => "websocket";

    public event Func<RawMessage, Task>? OnRawMessage;

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        _listeningTask = Task.Run(
            () => ListenLoopAsync(cancellationToken),
            cancellationToken
        );

        return Task.CompletedTask;
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (_clientWebSocket is not null)
        {
            try
            {
                await _clientWebSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Connector stopping",
                    cancellationToken
                );
            }
            catch
            {
                // Ignore close errors during shutdown.
            }

            _clientWebSocket.Dispose();
        }
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _clientWebSocket?.Dispose();
                _clientWebSocket = new ClientWebSocket();

                _logger.LogInformation(
                    "Connecting to WebSocket source: {Url}",
                    _webSocketUrl
                );

                await _clientWebSocket.ConnectAsync(
                    new Uri(_webSocketUrl),
                    cancellationToken
                );

                _logger.LogInformation("Connected to WebSocket source.");

                await ReceiveMessagesAsync(_clientWebSocket, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "WebSocket connection failed. Retrying in 3 seconds."
                );

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ReceiveMessagesAsync(
        ClientWebSocket webSocket,
        CancellationToken cancellationToken
    )
    {
        var buffer = new byte[8192];

        while (
            webSocket.State == WebSocketState.Open &&
            !cancellationToken.IsCancellationRequested
        )
        {
            var result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationToken
            );

            if (result.MessageType == WebSocketMessageType.Close)
            {
                _logger.LogWarning("WebSocket source closed the connection.");
                break;
            }

            var payload = Encoding.UTF8.GetString(buffer, 0, result.Count);

            if (OnRawMessage is not null)
            {
                var rawMessage = new RawMessage(
                    Source: "websocket",
                    Protocol: "websocket",
                    Payload: payload,
                    ReceivedAt: DateTimeOffset.UtcNow
                );

                await OnRawMessage.Invoke(rawMessage);
            }
        }
    }
}