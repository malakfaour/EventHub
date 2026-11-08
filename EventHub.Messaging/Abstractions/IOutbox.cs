using EventHub.Contracts;

namespace EventHub.Messaging;

/// <summary>
/// Writes a message to the app's outbox table. It only ADDS to the current unit of work, so the caller's
/// single SaveChanges commits the data change and the message together.
/// </summary>
public interface IOutbox
{
    void Enqueue<T>(T message) where T : MessageBase;
}

public sealed record OutboxItem(Guid Id, string Type, string Payload, Guid CorrelationId);

/// <summary>Read side of the outbox, used by <see cref="OutboxPublisher"/>.</summary>
public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxItem>> GetPendingAsync(int take, CancellationToken ct);
    Task MarkPublishedAsync(Guid id, CancellationToken ct);
    Task MarkFailedAsync(Guid id, string error, CancellationToken ct);
}
