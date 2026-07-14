using System.Net.Http.Json;
using Connector.Models;

namespace Connector.Services;

public class BackendNotificationClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BackendNotificationClient> _logger;

    public BackendNotificationClient(
        HttpClient httpClient,
        ILogger<BackendNotificationClient> logger
    )
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task SendAsync(
        NotificationEnvelope notification,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/notifications",
                notification,
                cancellationToken
            );

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Notification sent to backend. Source: {Source}, Type: {Type}, DeduplicationKey: {DeduplicationKey}",
                    notification.Source,
                    notification.Type,
                    notification.DeduplicationKey
                );

                return;
            }

            _logger.LogWarning(
                "Backend returned non-success status code: {StatusCode}",
                response.StatusCode
            );
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to send notification to backend. DeduplicationKey: {DeduplicationKey}",
                notification.DeduplicationKey
            );
        }
    }
}