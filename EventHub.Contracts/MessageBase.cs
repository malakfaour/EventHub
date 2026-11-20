namespace EventHub.Contracts;

/// <summary>
/// Envelope shared by every message. <see cref="MessageId"/> powers idempotency,
/// <see cref="CorrelationId"/> lets one business action be traced across both apps.
/// </summary>
public abstract record MessageBase
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public int Version { get; init; } = 1;
}

/// <summary>Messages that carry a supply category, so the routing key can end with it (order.placed.catering).</summary>
public interface ICategorized
{
    string Category { get; }
}
