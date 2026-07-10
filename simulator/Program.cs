using System.Net.Http.Json;

var backendUrl = Environment.GetEnvironmentVariable("BACKEND_URL")
    ?? "http://localhost:8080";

using var cancellationTokenSource = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellationTokenSource.Cancel();
};

using var httpClient = new HttpClient
{
    BaseAddress = new Uri(backendUrl)
};

var eventTypes = new[]
{
    "order.created",
    "payment.received",
    "user.registered",
    "stock.updated"
};

var counter = 1;

Console.WriteLine("Simulator started.");
Console.WriteLine($"Backend URL: {backendUrl}");

while (!cancellationTokenSource.Token.IsCancellationRequested)
{
    var eventType = eventTypes[counter % eventTypes.Length];

    var notification = new
    {
        source = "simulator-http",
        type = eventType,
        message = $"Test notification #{counter} - {eventType}",
        occurredAt = DateTimeOffset.UtcNow,
        deduplicationKey = $"simulator-http-{counter}"
    };

    try
    {
        var response = await httpClient.PostAsJsonAsync(
            "/api/notifications",
            notification,
            cancellationTokenSource.Token
        );

        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Sent notification #{counter}");
        }
        else
        {
            Console.WriteLine(
                $"Backend returned error for notification #{counter}: {(int)response.StatusCode}"
            );
        }
    }
    catch (OperationCanceledException)
    {
        break;
    }
    catch (Exception exception)
    {
        Console.WriteLine($"Failed to send notification #{counter}: {exception.Message}");
    }

    counter++;

    try
    {
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationTokenSource.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }
}

Console.WriteLine("Simulator stopped.");