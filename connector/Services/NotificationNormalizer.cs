using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Connector.Models;

namespace Connector.Services;

public class NotificationNormalizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public NotificationEnvelope Normalize(RawMessage rawMessage)
    {
        try
        {
            var incoming = JsonSerializer.Deserialize<IncomingProtocolMessage>(
                rawMessage.Payload,
                JsonOptions
            );

            if (incoming is null)
            {
                return CreateInvalidEnvelope(rawMessage, "Payload could not be parsed.");
            }

            var source = string.IsNullOrWhiteSpace(incoming.Source)
                ? rawMessage.Source
                : incoming.Source;

            var type = string.IsNullOrWhiteSpace(incoming.Type)
                ? "unknown"
                : incoming.Type;

            var message = string.IsNullOrWhiteSpace(incoming.Message)
                ? rawMessage.Payload
                : incoming.Message;

            var occurredAt = incoming.OccurredAt ?? rawMessage.ReceivedAt;

            var deduplicationKey = !string.IsNullOrWhiteSpace(incoming.DeduplicationKey)
                ? incoming.DeduplicationKey
                : CreateDeterministicKey(source, type, message, occurredAt);

            return new NotificationEnvelope(
                Source: source,
                Type: type,
                Message: message,
                OccurredAt: occurredAt,
                DeduplicationKey: deduplicationKey
            );
        }
        catch
        {
            return CreateInvalidEnvelope(rawMessage, "Invalid message payload.");
        }
    }

    private static NotificationEnvelope CreateInvalidEnvelope(RawMessage rawMessage, string reason)
    {
        var message = $"Invalid raw message from {rawMessage.Source}. Reason: {reason}";

        return new NotificationEnvelope(
            Source: rawMessage.Source,
            Type: "invalid.message",
            Message: message,
            OccurredAt: rawMessage.ReceivedAt,
            DeduplicationKey: CreateDeterministicKey(
                rawMessage.Source,
                "invalid.message",
                rawMessage.Payload,
                rawMessage.ReceivedAt
            )
        );
    }

    private static string CreateDeterministicKey(
        string source,
        string type,
        string message,
        DateTimeOffset occurredAt
    )
    {
        var rawKey = $"{source}|{type}|{message}|{occurredAt:O}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}