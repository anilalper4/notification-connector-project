using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var connectedWebSocketClients = new ConcurrentDictionary<Guid, WebSocket>();

var connectorWebhookUrl = Environment.GetEnvironmentVariable("CONNECTOR_WEBHOOK_URL")
    ?? "http://localhost:8090/webhook";

var simulatorPort = Environment.GetEnvironmentVariable("PORT") ?? "7001";

var rabbitMqUri = Environment.GetEnvironmentVariable("RABBITMQ_URI")
    ?? "amqp://guest:guest@localhost:5672";

var rabbitMqQueue = Environment.GetEnvironmentVariable("RABBITMQ_QUEUE")
    ?? "notifications.rabbitmq";

var redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING")
    ?? "localhost:6379";

var redisChannel = Environment.GetEnvironmentVariable("REDIS_CHANNEL")
    ?? "notifications.redis";

app.Urls.Add($"http://0.0.0.0:{simulatorPort}");

app.UseWebSockets();

app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        status = "Simulator is running",
        service = "notification-simulator",
        websocketEndpoint = "/ws",
        connectorWebhookUrl,
        rabbitMqUri,
        rabbitMqQueue,
        redisConnectionString,
        redisChannel
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
        rabbitMqUri,
        rabbitMqQueue,
        redisConnectionString,
        redisChannel,
        app.Lifetime.ApplicationStopping
    );
});

Console.WriteLine("Simulator started.");
Console.WriteLine($"WebSocket URL: ws://localhost:{simulatorPort}/ws");
Console.WriteLine($"Connector webhook URL: {connectorWebhookUrl}");
Console.WriteLine($"RabbitMQ URI: {rabbitMqUri}");
Console.WriteLine($"RabbitMQ queue: {rabbitMqQueue}");
Console.WriteLine($"Redis connection: {redisConnectionString}");
Console.WriteLine($"Redis channel: {redisChannel}");

await app.RunAsync();

static async Task ProduceMessagesAsync(
    ConcurrentDictionary<Guid, WebSocket> connectedWebSocketClients,
    string connectorWebhookUrl,
    string rabbitMqUri,
    string rabbitMqQueue,
    string redisConnectionString,
    string redisChannel,
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
        var occurredAt = DateTimeOffset.UtcNow;

        var webhookNotification = new
        {
            source = "simulator-webhook",
            type = eventType,
            message = $"Webhook notification #{counter} - {eventType}",
            occurredAt,
            deduplicationKey = $"simulator-webhook-{counter}"
        };

        var webSocketNotification = new
        {
            source = "simulator-websocket",
            type = eventType,
            message = $"WebSocket notification #{counter} - {eventType}",
            occurredAt,
            deduplicationKey = $"simulator-websocket-{counter}"
        };

        var rabbitMqNotification = new
        {
            source = "simulator-rabbitmq",
            type = eventType,
            message = $"RabbitMQ notification #{counter} - {eventType}",
            occurredAt,
            deduplicationKey = $"simulator-rabbitmq-{counter}"
        };

        var redisNotification = new
        {
            source = "simulator-redis",
            type = eventType,
            message = $"Redis notification #{counter} - {eventType}",
            occurredAt,
            deduplicationKey = $"simulator-redis-{counter}"
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

        await PublishRabbitMqMessageAsync(
            rabbitMqUri,
            rabbitMqQueue,
            rabbitMqNotification,
            counter
        );

        await PublishRedisMessageAsync(
            redisConnectionString,
            redisChannel,
            redisNotification,
            counter
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

static Task PublishRabbitMqMessageAsync(
    string rabbitMqUri,
    string queueName,
    object notification,
    int counter
)
{
    try
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(rabbitMqUri)
        };

        using var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        var payload = JsonSerializer.Serialize(notification);
        var body = Encoding.UTF8.GetBytes(payload);

        channel.BasicPublish(
            exchange: string.Empty,
            routingKey: queueName,
            basicProperties: null,
            body: body
        );

        Console.WriteLine($"RabbitMQ notification published: #{counter}");
    }
    catch (Exception exception)
    {
        Console.WriteLine($"RabbitMQ notification error #{counter}: {exception.Message}");
    }

    return Task.CompletedTask;
}

static async Task PublishRedisMessageAsync(
    string redisConnectionString,
    string channelName,
    object notification,
    int counter
)
{
    try
    {
        var connection = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
        var subscriber = connection.GetSubscriber();

        var payload = JsonSerializer.Serialize(notification);

        await subscriber.PublishAsync(
            RedisChannel.Literal(channelName),
            payload
        );

        await connection.CloseAsync();
        await connection.DisposeAsync();

        Console.WriteLine($"Redis notification published: #{counter}");
    }
    catch (Exception exception)
    {
        Console.WriteLine($"Redis notification error #{counter}: {exception.Message}");
    }
}