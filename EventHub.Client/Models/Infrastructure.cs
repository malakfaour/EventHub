namespace EventHub.Client.Models;

/// <summary>Outbox: a message saved in the same transaction as the data change, sent later by OutboxPublisher.</summary>
public class OutboxMessage
{
    public Guid Id { get; set; }              // equals the message's MessageId
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "";
    public Guid CorrelationId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

/// <summary>Idempotency: which messages this consumer has already applied.</summary>
public class ProcessedMessage
{
    public Guid MessageId { get; set; }
    public string ConsumerName { get; set; } = "";
    public DateTime ProcessedAtUtc { get; set; }
}

/// <summary>Audit trail of published, consumed, retried, failed and dead-lettered messages.</summary>
public class MessageLogRow
{
    public long Id { get; set; }
    public Guid? MessageId { get; set; }
    public Guid? CorrelationId { get; set; }
    public string Type { get; set; } = "";
    public string Direction { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Queue { get; set; }
    public string? Error { get; set; }
    public DateTime TimestampUtc { get; set; }
}
