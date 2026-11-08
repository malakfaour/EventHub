namespace EventHub.Messaging;

/// <summary>Bound from the "RabbitMq" configuration section (appsettings, user secrets or env vars).</summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = "eventhub";
    public string Password { get; set; } = "";
    public string ClientName { get; set; } = "eventhub-app";

    /// <summary>How long a failed message waits in the retry queue before returning to its main queue.</summary>
    public int RetryDelayMs { get; set; } = 5000;
    public int MaxRetries { get; set; } = 3;
    /// <summary>Max unacknowledged messages per consumer (BasicQos).</summary>
    public ushort Prefetch { get; set; } = 10;
}
