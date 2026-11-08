using EventHub.Client.Services;
using EventHub.Contracts;
using EventHub.Messaging;
using Microsoft.Extensions.Options;

namespace EventHub.Client.Messaging;

/// <summary>Consumes the Admin App's decisions from client.moderation-results.</summary>
public sealed class ModerationResultsConsumer : BaseConsumer
{
    public ModerationResultsConsumer(IRabbitMqConnection connection, TopologyDeclarer topology,
        IServiceScopeFactory scopes, IOptions<RabbitMqOptions> options, ILogger<ModerationResultsConsumer> logger)
        : base(connection, topology, scopes, options, logger) { }

    protected override string QueueName => Topology.Queues.ClientModerationResults;

    protected override async Task HandleAsync(MessageBase message, IServiceProvider services, CancellationToken ct)
    {
        var events = services.GetRequiredService<IEventService>();
        switch (message)
        {
            case EventApproved approved:
                await events.ApplyApprovedAsync(approved, ct);
                break;
            case EventRejected rejected:
                await events.ApplyRejectedAsync(rejected, ct);
                break;
            default:
                // ReportResolved arrives in a later phase; until then it goes to retry and the dead-letter queue.
                throw new NotSupportedException($"{message.GetType().Name} is not handled yet.");
        }
    }
}
