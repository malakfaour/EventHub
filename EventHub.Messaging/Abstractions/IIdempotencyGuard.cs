namespace EventHub.Messaging;

/// <summary>
/// Implemented by each app on top of its own database. Runs <paramref name="work"/> and records the
/// MessageId in ProcessedMessages inside ONE transaction, so a message is applied at most once.
/// </summary>
public interface IIdempotencyGuard
{
    /// <returns>True if the work ran; false if this MessageId was already processed (duplicate).</returns>
    Task<bool> ExecuteOnceAsync(Guid messageId, string consumerName,
        Func<CancellationToken, Task> work, CancellationToken ct);
}
