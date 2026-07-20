using Connector.Adapters;
using Connector.Contracts;
using Connector.Core;
using Connector.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<NotificationNormalizer>();
builder.Services.AddSingleton<IConnector, NotificationConnector>();

builder.Services.AddSingleton<WebhookSourceAdapter>();
builder.Services.AddSingleton<WebSocketSourceAdapter>();
builder.Services.AddSingleton<RabbitMqSourceAdapter>();
builder.Services.AddSingleton<RedisSourceAdapter>();
builder.Services.AddHttpClient<BackendNotificationClient>(client =>
{
    var backendUrl = builder.Configuration["BACKEND_URL"]
        ?? "http://localhost:8080";

    client.BaseAddress = new Uri(backendUrl);
});

builder.Services.AddHostedService<ConnectorHostedService>();

var app = builder.Build();

app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        status = "Connector is running",
        service = "notification-connector"
    });
});

app.MapPost("/webhook", async (
    HttpRequest request,
    WebhookSourceAdapter webhookSourceAdapter
) =>
{
    using var reader = new StreamReader(request.Body);
    var payload = await reader.ReadToEndAsync();

    await webhookSourceAdapter.ReceiveAsync(payload);

    return Results.Ok(new
    {
        accepted = true
    });
});

var port = Environment.GetEnvironmentVariable("PORT") ?? "8090";
app.Urls.Add($"http://0.0.0.0:{port}");

app.Run();