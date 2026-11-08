using EventHub.Contracts;
using RabbitMQ.Client;

namespace EventHub.Messaging;

/// <summary>
/// Publishes persistent messages with publisher confirms: the returned task completes only after the broker
/// has accepted the message, so callers (the outbox) can safely mark it as sent.
/// </summary>
public sealed class RabbitMqPublisher : IMessagePublisher, IAsyncDisposable
{
    private readonly IRabbitMqConnection _connection;
    private readonly TopologyDeclarer _topology;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    public RabbitMqPublisher(IRabbitMqConnection connection, TopologyDeclarer topology)
    {
        _connection = connection;
        _topology = topology;
    }

    public Task PublishAsync<T>(T message, CancellationToken ct = default) where T : MessageBase
    {
        var route = MessageRoutes.For<T>();
        return PublishRawAsync(route.Exchange, route.RoutingKey, typeof(T).Name,
            message.MessageId, message.CorrelationId, MessageSerializer.Serialize(message), ct);
    }

    public async Task PublishRawAsync(string exchange, string routingKey, string typeName, Guid messageId,
        Guid correlationId, ReadOnlyMemory<byte> body, CancellationToken ct = default)
    {
        await _topology.EnsureDeclaredAsync(ct);

        var properties = new BasicProperties
        {
            Persistent = true,                       // delivery-mode 2: survives a broker restart
            ContentType = "application/json",
            MessageId = messageId.ToString(),
            CorrelationId = correlationId.ToString(),
            Type = typeName,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        // A channel with confirm tracking is not safe for concurrent publishes, so publishes are serialized.
        await _gate.WaitAsync(ct);
        try
        {
            var channel = await GetChannelAsync(ct);
            await channel.BasicPublishAsync(exchange, routingKey, mandatory: false, properties, body, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true }) return _channel;

        if (_channel is not null) await _channel.DisposeAsync();
        var connection = await _connection.GetAsync(ct);
        _channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            ct);
        return _channel;
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        _gate.Dispose();
    }
}
