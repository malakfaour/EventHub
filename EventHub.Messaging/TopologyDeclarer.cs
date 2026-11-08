using EventHub.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace EventHub.Messaging;

/// <summary>
/// Declares exchanges, main queues, retry queues and dead-letter queues. Declaring is idempotent, and BOTH apps
/// declare the whole topology, so a message published while the other app has never run still finds its queue.
/// </summary>
public sealed class TopologyDeclarer
{
    private readonly IRabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<TopologyDeclarer> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _declared;

    public TopologyDeclarer(IRabbitMqConnection connection, IOptions<RabbitMqOptions> options, ILogger<TopologyDeclarer> logger)
    {
        _connection = connection;
        _options = options.Value;
        _logger = logger;
    }

    public async Task EnsureDeclaredAsync(CancellationToken ct = default)
    {
        if (_declared) return;
        await _gate.WaitAsync(ct);
        try
        {
            if (_declared) return;
            var connection = await _connection.GetAsync(ct);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);
            await DeclareAsync(channel, ct);
            _declared = true;
            _logger.LogInformation("RabbitMQ topology declared ({Count} queues)", Topology.Bindings.Count);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task DeclareAsync(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(Topology.ClientExchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(Topology.AdminExchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        await channel.ExchangeDeclareAsync(Topology.DeadLetterExchange, ExchangeType.Direct, durable: true, cancellationToken: ct);

        foreach (var binding in Topology.Bindings)
        {
            var queue = binding.Queue;
            var retryQueue = Topology.RetryQueue(queue);
            var dlq = Topology.DeadLetterQueue(queue);

            // Main queue: anything rejected without requeue lands in the DLQ via the dead-letter exchange.
            await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-dead-letter-exchange"] = Topology.DeadLetterExchange,
                    ["x-dead-letter-routing-key"] = dlq
                }, cancellationToken: ct);

            // Retry queue: nobody consumes it. After the TTL the message expires and is dead-lettered
            // through the default exchange straight back into the main queue.
            await channel.QueueDeclareAsync(retryQueue, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = _options.RetryDelayMs,
                    ["x-dead-letter-exchange"] = "",
                    ["x-dead-letter-routing-key"] = queue
                }, cancellationToken: ct);

            await channel.QueueDeclareAsync(dlq, durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);

            await channel.QueueBindAsync(retryQueue, Topology.DeadLetterExchange, retryQueue, cancellationToken: ct);
            await channel.QueueBindAsync(dlq, Topology.DeadLetterExchange, dlq, cancellationToken: ct);

            foreach (var key in binding.RoutingKeys)
                await channel.QueueBindAsync(queue, binding.Exchange, key, cancellationToken: ct);
        }
    }
}
