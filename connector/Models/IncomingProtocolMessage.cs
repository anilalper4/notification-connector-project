namespace Connector.Models;

public record IncomingProtocolMessage(
    string? Source,
    string? Type,
    string? Message,
    DateTimeOffset? OccurredAt,
    string? DeduplicationKey
);