using EventHub.Client.Data;
using EventHub.Client.Models;
using EventHub.Contracts;
using EventHub.Messaging;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Client.Messaging;

/// <summary>EF Core implementation of the outbox (write side and read side).</summary>
public sealed class EfOutbox : IOutbox, IOutboxStore
{
    private readonly AppDbContext _db;

    public EfOutbox(AppDbContext db) => _db = db;

    public void Enqueue<T>(T message) where T : MessageBase
    {
        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = message.MessageId,
            Type = message.GetType().Name,
            Payload = MessageSerializer.SerializeToString(message),
            CorrelationId = message.CorrelationId,
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    public async Task<IReadOnlyList<OutboxItem>> GetPendingAsync(int take, CancellationToken ct) =>
        await _db.OutboxMessages.AsNoTracking()
            .Where(o => o.PublishedAtUtc == null)
            .OrderBy(o => o.CreatedAtUtc)
            .Take(take)
            .Select(o => new OutboxItem(o.Id, o.Type, o.Payload, o.CorrelationId))
            .ToListAsync(ct);

    public Task MarkPublishedAsync(Guid id, CancellationToken ct) =>
        _db.OutboxMessages.Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.PublishedAtUtc, DateTime.UtcNow), ct);

    public Task MarkFailedAsync(Guid id, string error, CancellationToken ct)
    {
        var text = error.Length > 1000 ? error[..1000] : error;
        return _db.OutboxMessages.Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Attempts, o => o.Attempts + 1)
                .SetProperty(o => o.LastError, text), ct);
    }
}
