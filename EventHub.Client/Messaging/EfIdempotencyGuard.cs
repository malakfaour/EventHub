using EventHub.Client.Data;
using EventHub.Client.Models;
using EventHub.Messaging;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Client.Messaging;

/// <summary>
/// Applies a message at most once: the duplicate check, the handler's changes and the ProcessedMessages row
/// all commit in ONE database transaction. The handler shares this scope's DbContext, so it joins the transaction.
/// </summary>
public sealed class EfIdempotencyGuard : IIdempotencyGuard
{
    private readonly AppDbContext _db;

    public EfIdempotencyGuard(AppDbContext db) => _db = db;

    public async Task<bool> ExecuteOnceAsync(Guid messageId, string consumerName,
        Func<CancellationToken, Task> work, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var seen = await _db.ProcessedMessages
            .AnyAsync(p => p.MessageId == messageId && p.ConsumerName == consumerName, ct);
        if (seen) return false;

        await work(ct);

        _db.ProcessedMessages.Add(new ProcessedMessage
        {
            MessageId = messageId,
            ConsumerName = consumerName,
            ProcessedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }
}
