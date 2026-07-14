namespace Connector.Models;

public record RawMessage(
    string Source,
    string Protocol,
    string Payload,
    DateTimeOffset ReceivedAt
);