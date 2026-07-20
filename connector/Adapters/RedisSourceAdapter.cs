using Connector.Contracts;
using Connector.Models;
using StackExchange.Redis;

namespace Connector.Adapters;

public class RedisSourceAdapter : ISourceAdapter
{
    private readonly ILogger<RedisSourceAdapter> _logger;
    private readonly string _redisConnectionString;
    private readonly string _channelName;

    private ConnectionMultiplexer? _connection;
    private ISubscriber? _subscriber;
    private ChannelMessageQueue? _messageQueue;

    public RedisSourceAdapter(
        IConfiguration configuration,
        ILogger<RedisSourceAdapter> logger
    )
    {
        _logger = logger;

        _redisConnectionString = configuration["REDIS_CONNECTION_STRING"]
            ?? "localhost:6379";

        _channelName = configuration["REDIS_CHANNEL"]
            ?? "notifications.redis";
    }

    public string Name => "redis";

    public event Func<RawMessage, Task>? OnRawMessage;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _connection = await ConnectionMultiplexer.ConnectAsync(
            _redisConnectionString
        );

        _subscriber = _connection.GetSubscriber();

        _messageQueue = await _subscriber.SubscribeAsync(
            RedisChannel.Literal(_channelName)
        );

        _messageQueue.OnMessage(async channelMessage =>
        {
            try
            {
                var payload = channelMessage.Message.ToString();

                if (OnRawMessage is null)
                {
                    _logger.LogWarning(
                        "Redis message ignored because no handler is registered."
                    );

                    return;
                }

                var rawMessage = new RawMessage(
                    Source: "redis",
                    Protocol: "redis",
                    Payload: payload,
                    ReceivedAt: DateTimeOffset.UtcNow
                );

                await OnRawMessage.Invoke(rawMessage);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Redis message processing failed."
                );
            }
        });

        _logger.LogInformation(
            "Redis adapter connected. Channel: {ChannelName}",
            _channelName
        );
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        if (_subscriber is not null)
        {
            await _subscriber.UnsubscribeAsync(
                RedisChannel.Literal(_channelName)
            );
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        _logger.LogInformation("Redis adapter disconnected.");
    }
}