using EventHub.Contracts;
using EventHub.Messaging;
using EventHub.Web.Services;
using Microsoft.Extensions.Options;

namespace EventHub.Web.Messaging;

/// <summary>Order status changes and rejections coming back from the Supplier System.</summary>
public sealed class OrderUpdatesConsumer : BaseConsumer
{
    public OrderUpdatesConsumer(IRabbitMqConnection connection, TopologyDeclarer topology, IServiceScopeFactory scopes,
        IOptions<RabbitMqOptions> options, ILogger<OrderUpdatesConsumer> logger)
        : base(connection, topology, scopes, options, logger) { }

    protected override string QueueName => Topology.Queues.EventHubOrderUpdates;

    protected override async Task HandleAsync(MessageBase message, IServiceProvider services, CancellationToken ct)
    {
        var orders = services.GetRequiredService<IOrderService>();
        switch (message)
        {
            case OrderStatusChanged changed: await orders.ApplyStatusAsync(changed, ct); break;
            case OrderRejected rejected: await orders.ApplyRejectedAsync(rejected, ct); break;
            default: throw new NotSupportedException($"{message.GetType().Name} is not handled here.");
        }
    }
}

/// <summary>Catalog snapshots and availability changes published by the suppliers.</summary>
public sealed class CatalogConsumer : BaseConsumer
{
    public CatalogConsumer(IRabbitMqConnection connection, TopologyDeclarer topology, IServiceScopeFactory scopes,
        IOptions<RabbitMqOptions> options, ILogger<CatalogConsumer> logger)
        : base(connection, topology, scopes, options, logger) { }

    protected override string QueueName => Topology.Queues.EventHubCatalog;

    protected override async Task HandleAsync(MessageBase message, IServiceProvider services, CancellationToken ct)
    {
        var catalog = services.GetRequiredService<ICatalogService>();
        switch (message)
        {
            case CatalogPublished published: await catalog.ApplyPublishedAsync(published, ct); break;
            case ItemAvailabilityChanged changed: await catalog.ApplyAvailabilityAsync(changed, ct); break;
            default: throw new NotSupportedException($"{message.GetType().Name} is not handled here.");
        }
    }
}

/// <summary>Invoices issued by suppliers after delivery.</summary>
public sealed class InvoicesConsumer : BaseConsumer
{
    public InvoicesConsumer(IRabbitMqConnection connection, TopologyDeclarer topology, IServiceScopeFactory scopes,
        IOptions<RabbitMqOptions> options, ILogger<InvoicesConsumer> logger)
        : base(connection, topology, scopes, options, logger) { }

    protected override string QueueName => Topology.Queues.EventHubInvoices;

    protected override async Task HandleAsync(MessageBase message, IServiceProvider services, CancellationToken ct)
    {
        var invoices = services.GetRequiredService<IInvoiceService>();
        switch (message)
        {
            case InvoiceIssued issued: await invoices.ApplyIssuedAsync(issued, ct); break;
            default: throw new NotSupportedException($"{message.GetType().Name} is not handled here.");
        }
    }
}
