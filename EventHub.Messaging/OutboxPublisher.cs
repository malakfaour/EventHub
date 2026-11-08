using System.Text;
using EventHub.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventHub.Messaging;

/// <summary>
/// Outbox pattern, publishing side. Polls the outbox for rows not yet sent, publishes each one with a broker
/// confirm, then marks it published. A crash between save and publish just means the row is sent on restart.
/// Delivery is therefore at-least-once; consumers deduplicate by MessageId.
/// </summary>
public sealed class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IMessagePublisher _publisher;
    private readonly ILogger<OutboxPublisher> _logger;

    public OutboxPublisher(IServiceScopeFactory scopes, IMessagePublisher publisher, ILogger<OutboxPublisher> logger)
    {
        _scopes = scopes;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromSeconds(1);
            try
            {
                await PublishPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Outbox publish failed; retrying in 5s");
                delay = TimeSpan.FromSeconds(5);
            }

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <returns>Number of messages published in this pass.</returns>
    public async Task<int> PublishPendingAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var log = scope.ServiceProvider.GetRequiredService<IMessageLog>();

        var pending = await store.GetPendingAsync(20, ct);
        var sent = 0;

        foreach (var item in pending)
        {
            try
            {
                var type = MessageRoutes.Resolve(item.Type)
                           ?? throw new InvalidOperationException($"Unknown outbox message type '{item.Type}'.");
                var route = MessageRoutes.For(type);

                await _publisher.PublishRawAsync(route.Exchange, route.RoutingKey, item.Type, item.Id,
                    item.CorrelationId, Encoding.UTF8.GetBytes(item.Payload), ct);

                await store.MarkPublishedAsync(item.Id, ct);
                await log.RecordAsync(new MessageLogEntry(item.Id, item.Type, MessageDirection.Published,
                    MessageStatus.Published, route.Exchange, null, item.CorrelationId), ct);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await store.MarkFailedAsync(item.Id, ex.Message, ct);
                throw; // stop the batch so message order is preserved; the loop backs off and retries
            }
        }

        return sent;
    }
}
