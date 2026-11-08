using EventHub.Contracts;

namespace EventHub.Messaging;

public interface IMessagePublisher
{
    /// <summary>Publishes a message to the exchange and routing key registered for its type, waiting for the broker confirm.</summary>
    Task PublishAsync<T>(T message, CancellationToken ct = default) where T : MessageBase;

    /// <summary>Lower-level publish used by the outbox, which stores the serialized payload.</summary>
    Task PublishRawAsync(string exchange, string routingKey, string typeName, Guid messageId,
        Guid correlationId, ReadOnlyMemory<byte> body, CancellationToken ct = default);
}
