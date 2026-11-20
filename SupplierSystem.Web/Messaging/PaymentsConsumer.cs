using EventHub.Contracts;
using EventHub.Messaging;
using Microsoft.Extensions.Options;
using SupplierSystem.Web.Services;

namespace SupplierSystem.Web.Messaging;

/// <summary>Reads supplier.payments: the Event Hub tells us an invoice was paid.</summary>
public sealed class PaymentsConsumer : BaseConsumer
{
    public PaymentsConsumer(IRabbitMqConnection connection, TopologyDeclarer topology, IServiceScopeFactory scopes,
        IOptions<RabbitMqOptions> options, ILogger<PaymentsConsumer> logger)
        : base(connection, topology, scopes, options, logger) { }

    protected override string QueueName => Topology.Queues.SupplierPayments;

    protected override async Task HandleAsync(MessageBase message, IServiceProvider services, CancellationToken ct)
    {
        var invoices = services.GetRequiredService<IInvoiceService>();
        switch (message)
        {
            case PaymentCompleted paid: await invoices.ApplyPaymentAsync(paid, ct); break;
            default: throw new NotSupportedException($"{message.GetType().Name} is not handled here.");
        }
    }
}
