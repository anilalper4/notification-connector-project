using System.Text;
using Connector.Contracts;
using Connector.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Connector.Adapters;

public class RabbitMqSourceAdapter : ISourceAdapter
{
    private readonly ILogger<RabbitMqSourceAdapter> _logger;
    private readonly string _rabbitMqUri;
    private readonly string _queueName;

    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqSourceAdapter(
        IConfiguration configuration,
        ILogger<RabbitMqSourceAdapter> logger
    )
    {
        _logger = logger;

        _rabbitMqUri = configuration["RABBITMQ_URI"]
            ?? "amqp://guest:guest@localhost:5672";

        _queueName = configuration["RABBITMQ_QUEUE"]
            ?? "notifications.rabbitmq";
    }

    public string Name => "rabbitmq";

    public event Func<RawMessage, Task>? OnRawMessage;

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_rabbitMqUri),
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.QueueDeclare(
            queue: _queueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null
        );

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.Received += async (_, eventArgs) =>
        {
            var payload = Encoding.UTF8.GetString(eventArgs.Body.ToArray());

            try
            {
                if (OnRawMessage is not null)
                {
                    var rawMessage = new RawMessage(
                        Source: "rabbitmq",
                        Protocol: "rabbitmq",
                        Payload: payload,
                        ReceivedAt: DateTimeOffset.UtcNow
                    );

                    await OnRawMessage.Invoke(rawMessage);
                }

                _channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "RabbitMQ message processing failed."
                );

                _channel.BasicNack(
                    eventArgs.DeliveryTag,
                    multiple: false,
                    requeue: true
                );
            }
        };

        _channel.BasicConsume(
            queue: _queueName,
            autoAck: false,
            consumer: consumer
        );

        _logger.LogInformation(
            "RabbitMQ adapter connected. Queue: {QueueName}",
            _queueName
        );

        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken)
    {
        _channel?.Close();
        _channel?.Dispose();

        _connection?.Close();
        _connection?.Dispose();

        _logger.LogInformation("RabbitMQ adapter disconnected.");

        return Task.CompletedTask;
    }
}