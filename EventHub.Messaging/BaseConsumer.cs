using System.Text;
using EventHub.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EventHub.Messaging;

/// <summary>
/// Template Method base class for every consumer. One queue can carry several message types, so the concrete
/// type is read from the AMQP 'type' property and resolved through <see cref="MessageRoutes"/>. The skeleton is fixed:
/// receive -> deserialize -> idempotency check + handle in one transaction -> ack,
/// and on failure -> retry queue (max N times) -> dead-letter queue.
/// Subclasses only supply the queue name and <see cref="HandleAsync"/>.
/// </summary>
public abstract class BaseConsumer : BackgroundService
{
    private const string RetryHeader = "x-retry-count";

    private readonly IRabbitMqConnection _connection;
    private readonly TopologyDeclarer _topology;
    private readonly IServiceScopeFactory _scopes;
    private readonly RabbitMqOptions _options;
    private readonly ILogger _logger;

    protected BaseConsumer(IRabbitMqConnection connection, TopologyDeclarer topology,
        IServiceScopeFactory scopes, IOptions<RabbitMqOptions> options, ILogger logger)
    {
        _connection = connection;
        _topology = topology;
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>The main queue this consumer reads from.</summary>
    protected abstract string QueueName { get; }

    /// <summary>Stored with each processed MessageId so two consumers can handle the same message independently.</summary>
    protected virtual string ConsumerName => GetType().Name;

    /// <summary>Applies the message to this app's database. Runs inside the idempotency transaction.</summary>
    protected abstract Task HandleAsync(MessageBase message, IServiceProvider services, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Keep trying until the broker is reachable; the app itself never depends on it to start.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Consumer} could not start or lost its channel; retrying in 5s", ConsumerName);
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        await _topology.EnsureDeclaredAsync(stoppingToken);
        var connection = await _connection.GetAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Prefetch: the broker never sends more than N unacknowledged messages at once.
        await channel.BasicQosAsync(0, _options.Prefetch, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, ea) => OnReceivedAsync(channel, ea, stoppingToken);

        // autoAck: false -> manual acknowledgement; the broker keeps the message until we ack.
        await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
        _logger.LogInformation("{Consumer} listening on {Queue}", ConsumerName, QueueName);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task OnReceivedAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken ct)
    {
        Guid.TryParse(ea.BasicProperties.MessageId, out var propMessageId);
        var typeName = ea.BasicProperties.Type ?? "(unknown)";

        try
        {
            var messageType = MessageRoutes.Resolve(typeName)
                              ?? throw new InvalidDataException($"Unknown message type '{typeName}'.");
            var message = MessageSerializer.Deserialize(Encoding.UTF8.GetString(ea.Body.Span), messageType) as MessageBase
                          ?? throw new InvalidDataException("Message body deserialized to null.");

            using (var scope = _scopes.CreateScope())
            {
                var guard = scope.ServiceProvider.GetRequiredService<IIdempotencyGuard>();
                var ran = await guard.ExecuteOnceAsync(message.MessageId, ConsumerName,
                    token => HandleAsync(message, scope.ServiceProvider, token), ct);

                if (ran)
                    await SafeLogAsync(new MessageLogEntry(message.MessageId, typeName, MessageDirection.Consumed,
                        MessageStatus.Consumed, QueueName, null, message.CorrelationId));
                else
                    _logger.LogInformation("Duplicate {Type} {MessageId} skipped", typeName, message.MessageId);
            }

            // Ack only AFTER the database transaction committed.
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down: leave the message unacked so the broker redelivers it.
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(channel, ea, typeName, propMessageId, ex, ct);
        }
    }

    private async Task HandleFailureAsync(IChannel channel, BasicDeliverEventArgs ea, string typeName,
        Guid messageId, Exception error, CancellationToken ct)
    {
        var retries = ReadRetryCount(ea.BasicProperties);
        var poison = error is InvalidDataException or System.Text.Json.JsonException;
        var exhausted = poison || retries >= _options.MaxRetries;

        var targetQueue = exhausted ? Topology.DeadLetterQueue(QueueName) : Topology.RetryQueue(QueueName);
        var errorText = error.Message.Length > 500 ? error.Message[..500] : error.Message;

        try
        {
            var props = new BasicProperties
            {
                Persistent = true,
                ContentType = ea.BasicProperties.ContentType,
                MessageId = ea.BasicProperties.MessageId,
                CorrelationId = ea.BasicProperties.CorrelationId,
                Type = ea.BasicProperties.Type,
                Headers = new Dictionary<string, object?>
                {
                    [RetryHeader] = exhausted ? retries : retries + 1,
                    ["x-last-error"] = errorText,
                    ["x-original-queue"] = QueueName
                }
            };

            // Routing key == queue name, bound on the dead-letter exchange.
            await channel.BasicPublishAsync(Topology.DeadLetterExchange, targetQueue, mandatory: false, props, ea.Body, ct);
            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);

            _logger.LogWarning(error, "{Type} {MessageId} failed (attempt {Attempt}); sent to {Target}",
                typeName, messageId, retries + 1, targetQueue);

            await SafeLogAsync(new MessageLogEntry(messageId == Guid.Empty ? null : messageId, typeName,
                MessageDirection.Consumed, exhausted ? MessageStatus.DeadLettered : MessageStatus.Retried,
                QueueName, errorText));
        }
        catch (Exception publishError)
        {
            // Could not even park the message: put it back so nothing is lost.
            _logger.LogError(publishError, "Could not move {Type} {MessageId} to {Target}; requeueing", typeName, messageId, targetQueue);
            await SafeLogAsync(new MessageLogEntry(messageId == Guid.Empty ? null : messageId, typeName,
                MessageDirection.Consumed, MessageStatus.Failed, QueueName, publishError.Message));
            try { await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: ct); }
            catch { /* channel is gone; the broker redelivers anyway */ }
        }
    }

    private static int ReadRetryCount(IReadOnlyBasicProperties props)
    {
        if (props.Headers is not null && props.Headers.TryGetValue(RetryHeader, out var value))
        {
            return value switch
            {
                int i => i,
                long l => (int)l,
                short s => s,
                byte b => b,
                _ => 0
            };
        }
        return 0;
    }

    /// <summary>The audit log must never be the reason a message fails, and it uses its own scope/transaction.</summary>
    private async Task SafeLogAsync(MessageLogEntry entry)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var log = scope.ServiceProvider.GetRequiredService<IMessageLog>();
            await log.RecordAsync(entry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write message log entry");
        }
    }
}
