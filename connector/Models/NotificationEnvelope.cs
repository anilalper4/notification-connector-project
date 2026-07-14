namespace Connector.Models;

public record NotificationEnvelope(
    string Source,
    string Type,
    string Message,
    DateTimeOffset OccurredAt,
    string DeduplicationKey
);