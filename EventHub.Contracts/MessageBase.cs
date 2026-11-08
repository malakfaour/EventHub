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
