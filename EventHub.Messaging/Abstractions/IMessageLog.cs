namespace EventHub.Messaging;

public enum MessageDirection { Published, Consumed }

public enum MessageStatus { Published, Consumed, Retried, Failed, DeadLettered }

public sealed record MessageLogEntry(
    Guid? MessageId, string Type, MessageDirection Direction, MessageStatus Status,
    string? Queue = null, string? Error = null, Guid? CorrelationId = null);

/// <summary>Persists the audit trail shown on the Admin message-log page. Each app can implement it with its own DB.</summary>
public interface IMessageLog
{
    Task RecordAsync(MessageLogEntry entry, CancellationToken ct = default);
}

/// <summary>Default when an app has not provided its own implementation.</summary>
public sealed class NullMessageLog : IMessageLog
{
    public Task RecordAsync(MessageLogEntry entry, CancellationToken ct = default) => Task.CompletedTask;
}
