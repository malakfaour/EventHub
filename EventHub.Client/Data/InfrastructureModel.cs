using EventHub.Client.Models;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Client.Data;

public static class InfrastructureModel
{
    /// <summary>Maps the outbox, idempotency and message-log tables.</summary>
    public static void ConfigureMessaging(this ModelBuilder b)
    {
        b.Entity<OutboxMessage>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(100);
            e.HasIndex(x => x.PublishedAtUtc);
        });

        b.Entity<ProcessedMessage>(e =>
        {
            e.HasKey(x => new { x.MessageId, x.ConsumerName });
            e.Property(x => x.ConsumerName).HasMaxLength(100);
        });

        b.Entity<MessageLogRow>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(100);
            e.Property(x => x.Direction).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.Queue).HasMaxLength(100);
            e.Property(x => x.Error).HasMaxLength(1000);
            e.HasIndex(x => x.TimestampUtc);
        });
    }
}
