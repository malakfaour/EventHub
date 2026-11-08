using EventHub.Admin.Services;
using EventHub.Contracts;
using EventHub.Messaging;
using Microsoft.Extensions.Options;

namespace EventHub.Admin.Messaging;

/// <summary>Consumes member submissions from admin.moderation.</summary>
public sealed class ModerationConsumer : BaseConsumer
{
    public ModerationConsumer(IRabbitMqConnection connection, TopologyDeclarer topology,
        IServiceScopeFactory scopes, IOptions<RabbitMqOptions> options, ILogger<ModerationConsumer> logger)
        : base(connection, topology, scopes, options, logger) { }

    protected override string QueueName => Topology.Queues.AdminModeration;

    protected override async Task HandleAsync(MessageBase message, IServiceProvider services, CancellationToken ct)
    {
        var moderation = services.GetRequiredService<IModerationService>();
        switch (message)
        {
            case EventSubmitted submitted:
                await moderation.ReceiveSubmissionAsync(submitted, ct);
                break;
            default:
                // EventUpdated, EventCancelled and EventReported are added in later phases.
                throw new NotSupportedException($"{message.GetType().Name} is not handled yet.");
        }
    }
}
