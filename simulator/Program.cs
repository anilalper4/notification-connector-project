using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var connectedWebSocketClients = new ConcurrentDictionary<Guid, WebSocket>();

var connectorWebhookUrl = Environment.GetEnvironmentVariable("CONNECTOR_WEBHOOK_URL")
    ?? "http://localhost:8090/webhook";

var simulatorPort = Environment.GetEnvironmentVariable("PORT") ?? "7001";

app.Urls.Add($"http://0.0.0.0:{simulatorPort}");

app.UseWebSockets();

app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        status = "Simulator is running",
        service = "notification-simulator",
        websocketEndpoint = "/ws",
        connectorWebhookUrl
    });
});

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("WebSocket request expected.");
        return;
    }

    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

    var clientId = Guid.NewGuid();

    connectedWebSocketClients.TryAdd(clientId, webSocket);

    Console.WriteLine($"WebSocket client connected: {clientId}");

    var buffer = new byte[1024];

    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                context.RequestAborted
            );

            if (result.MessageType == WebSocketMessageType.Close)
            {
                break;
            }
        }
    }
    catch (OperationCanceledException)
    {
        // Normal shutdown.
    }
    catch (Exception exception)
    {
        Console.WriteLine($"WebSocket client error: {exception.Message}");
    }
    finally
    {
        connectedWebSocketClients.TryRemove(clientId, out _);

        Console.WriteLine($"WebSocket client disconnected: {clientId}");

        if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Simulator closing connection",
                CancellationToken.None
            );
        }
    }
});

_ = Task.Run(async () =>
{
    await ProduceMessagesAsync(
        connectedWebSocketClients,
        connectorWebhookUrl,
        app.Lifetime.ApplicationStopping
    );
});

Console.WriteLine("Simulator started.");
Console.WriteLine($"WebSocket URL: ws://localhost:{simulatorPort}/ws");
Console.WriteLine($"Connector webhook URL: {connectorWebhookUrl}");

await app.RunAsync();

static async Task ProduceMessagesAsync(
    ConcurrentDictionary<Guid, WebSocket> connectedWebSocketClients,
    string connectorWebhookUrl,
    CancellationToken cancellationToken
)
{
    using var httpClient = new HttpClient();

    var eventTypes = new[]
    {
        "order.created",
        "payment.received",
        "user.registered",
        "stock.updated"
    };

    var counter = 1;

    while (!cancellationToken.IsCancellationRequested)
    {
        var eventType = eventTypes[counter % eventTypes.Length];

        var webhookNotification = new
        {
            source = "simulator-webhook",
            type = eventType,
            message = $"Webhook notification #{counter} - {eventType}",
            occurredAt = DateTimeOffset.UtcNow,
            deduplicationKey = $"simulator-webhook-{counter}"
        };

        var webSocketNotification = new
        {
            source = "simulator-websocket",
            type = eventType,
            message = $"WebSocket notification #{counter} - {eventType}",
            occurredAt = DateTimeOffset.UtcNow,
            deduplicationKey = $"simulator-websocket-{counter}"
        };

        await SendWebhookMessageAsync(
            httpClient,
            connectorWebhookUrl,
            webhookNotification,
            counter,
            cancellationToken
        );

        await BroadcastWebSocketMessageAsync(
            connectedWebSocketClients,
            webSocketNotification,
            counter,
            cancellationToken
        );

        counter++;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            break;
        }
    }
}

static async Task SendWebhookMessageAsync(
    HttpClient httpClient,
    string connectorWebhookUrl,
    object notification,
    int counter,
    CancellationToken cancellationToken
)
{
    try
    {
        var response = await httpClient.PostAsJsonAsync(
            connectorWebhookUrl,
            notification,
            cancellationToken
        );

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Webhook notification sent: #{counter}");
        }
        else
        {
            Console.WriteLine(
                $"Webhook notification failed: #{counter}, StatusCode: {(int)response.StatusCode}"
            );
        }
    }
    catch (OperationCanceledException)
    {
        // Normal shutdown.
    }
    catch (Exception exception)
    {
        Console.WriteLine($"Webhook notification error #{counter}: {exception.Message}");
    }
}

static async Task BroadcastWebSocketMessageAsync(
    ConcurrentDictionary<Guid, WebSocket> connectedWebSocketClients,
    object notification,
    int counter,
    CancellationToken cancellationToken
)
{
    if (connectedWebSocketClients.IsEmpty)
    {
        Console.WriteLine($"No WebSocket clients connected for notification #{counter}");
        return;
    }

    var payload = JsonSerializer.Serialize(notification);
    var bytes = Encoding.UTF8.GetBytes(payload);

    foreach (var client in connectedWebSocketClients)
    {
        var webSocket = client.Value;

        if (webSocket.State != WebSocketState.Open)
        {
            connectedWebSocketClients.TryRemove(client.Key, out _);
            continue;
        }

        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cancellationToken
            );

            Console.WriteLine($"WebSocket notification sent: #{counter}");
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                $"WebSocket notification error #{counter}: {exception.Message}"
            );

            connectedWebSocketClients.TryRemove(client.Key, out _);
        }
    }
}