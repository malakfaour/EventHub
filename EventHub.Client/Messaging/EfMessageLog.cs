using EventHub.Client.Data;
using EventHub.Client.Models;
using EventHub.Messaging;

namespace EventHub.Client.Messaging;

public sealed class EfMessageLog : IMessageLog
{
    private readonly AppDbContext _db;

    public EfMessageLog(AppDbContext db) => _db = db;

    public async Task RecordAsync(MessageLogEntry entry, CancellationToken ct = default)
    {
        _db.MessageLog.Add(new MessageLogRow
        {
            MessageId = entry.MessageId,
            CorrelationId = entry.CorrelationId,
            Type = entry.Type,
            Direction = entry.Direction.ToString(),
            Status = entry.Status.ToString(),
            Queue = entry.Queue,
            Error = entry.Error,
            TimestampUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
