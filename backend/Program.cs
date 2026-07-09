using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true);
    });
});

var app = builder.Build();

app.UseCors();

var notifications = new ConcurrentDictionary<string, NotificationEnvelope>();

app.MapGet("/", () =>
{
    return Results.Ok(new
    {
        status = "Backend is running",
        service = "notification-backend"
    });
});

app.MapGet("/api/notifications", () =>
{
    var result = notifications.Values
        .OrderByDescending(notification => notification.ReceivedAt)
        .Take(100)
        .ToList();

    return Results.Ok(result);
});

app.MapPost("/api/notifications", CreateNotification);

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");

app.Run();

IResult CreateNotification(IncomingNotification request)
{
    if (string.IsNullOrWhiteSpace(request.Source))
    {
        return Results.BadRequest(new
        {
            error = "Source field is required."
        });
    }

    if (string.IsNullOrWhiteSpace(request.Message))
    {
        return Results.BadRequest(new
        {
            error = "Message field is required."
        });
    }

    var occurredAt = request.OccurredAt ?? DateTimeOffset.UtcNow;

    var deduplicationKey = !string.IsNullOrWhiteSpace(request.DeduplicationKey)
        ? request.DeduplicationKey
        : $"{request.Source}:{request.Type}:{request.Message}:{occurredAt:O}";

    var notification = new NotificationEnvelope(
        Id: Guid.NewGuid(),
        Source: request.Source,
        Type: string.IsNullOrWhiteSpace(request.Type) ? "unknown" : request.Type,
        Message: request.Message,
        OccurredAt: occurredAt,
        ReceivedAt: DateTimeOffset.UtcNow,
        DeduplicationKey: deduplicationKey
    );

    var added = notifications.TryAdd(deduplicationKey, notification);

    if (!added)
    {
        return Results.Ok(new
        {
            added = false,
            reason = "Duplicate notification ignored.",
            notification = notifications[deduplicationKey]
        });
    }

    return Results.Ok(new
    {
        added = true,
        notification
    });
}

public record IncomingNotification(
    string Source,
    string? Type,
    string Message,
    DateTimeOffset? OccurredAt,
    string? DeduplicationKey
);

public record NotificationEnvelope(
    Guid Id,
    string Source,
    string Type,
    string Message,
    DateTimeOffset OccurredAt,
    DateTimeOffset ReceivedAt,
    string DeduplicationKey
);