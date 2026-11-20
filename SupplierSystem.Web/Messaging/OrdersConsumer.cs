using EventHub.Contracts;
using EventHub.Messaging;
using Microsoft.Extensions.Options;
using SupplierSystem.Web.Services;

namespace SupplierSystem.Web.Messaging;

/// <summary>
/// Reads one category's order queue (supplier.orders.catering, ...). One instance runs per category,
/// each as its own background service, so a slow category never blocks the others.
/// </summary>
public sealed class OrdersConsumer : BaseConsumer
{
    private readonly string _queue;

    public OrdersConsumer(string queue, IRabbitMqConnection connection, TopologyDeclarer topology,
        IServiceScopeFactory scopes, IOptions<RabbitMqOptions> options, ILogger<OrdersConsumer> logger)
        : base(connection, topology, scopes, options, logger) => _queue = queue;

    protected override string QueueName => _queue;

    // Part of the idempotency key: the same message in two different queues is two different jobs.
    protected override string ConsumerName => $"{nameof(OrdersConsumer)}:{_queue}";

    protected override async Task HandleAsync(MessageBase message, IServiceProvider services, CancellationToken ct)
    {
        var orders = services.GetRequiredService<IOrderService>();
        switch (message)
        {
            case SupplyOrderPlaced placed:
                await orders.ReceiveAsync(placed, ct);
                break;
            case OrderCancelled cancelled:
                await orders.ApplyCancelledAsync(cancelled, ct);
                break;
            default:
                throw new NotSupportedException($"{message.GetType().Name} is not handled on an order queue.");
        }
    }
}
