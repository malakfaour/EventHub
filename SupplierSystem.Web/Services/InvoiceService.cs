using EventHub.Contracts;
using EventHub.Messaging;
using SupplierSystem.Web.Models;
using SupplierSystem.Web.Repositories;

namespace SupplierSystem.Web.Services;

public interface IInvoiceService
{
    Task<List<Invoice>> ListAsync(Guid? supplierScope, CancellationToken ct = default);
    Task<Invoice?> GetByOrderAsync(Guid orderId, Guid? supplierScope, CancellationToken ct = default);

    /// <summary>Creates the invoice for a delivered order and queues InvoiceIssued, in one transaction.</summary>
    Task IssueAsync(Guid orderId, Guid? supplierScope, CancellationToken ct = default);

    /// <summary>The Event Hub paid: mark the invoice as paid (safe to receive twice).</summary>
    Task ApplyPaymentAsync(PaymentCompleted message, CancellationToken ct = default);
}

public sealed class InvoiceService : IInvoiceService
{
    private const int PaymentTermDays = 14;

    private readonly IInvoiceRepository _invoices;
    private readonly IOrderRepository _orders;
    private readonly IOutbox _outbox;
    private readonly ILogger<InvoiceService> _logger;
    private readonly IRealtimeNotifier _live;

    public InvoiceService(IInvoiceRepository invoices, IOrderRepository orders, IOutbox outbox, ILogger<InvoiceService> logger,
        IRealtimeNotifier live)
    {
        _live = live;
        _invoices = invoices;
        _orders = orders;
        _outbox = outbox;
        _logger = logger;
    }

    public Task<List<Invoice>> ListAsync(Guid? supplierScope, CancellationToken ct = default) =>
        _invoices.ListAsync(supplierScope, ct);

    public async Task<Invoice?> GetByOrderAsync(Guid orderId, Guid? supplierScope, CancellationToken ct = default)
    {
        var invoice = await _invoices.GetByOrderAsync(orderId, ct);
        return invoice is not null && (supplierScope is null || invoice.SupplierId == supplierScope) ? invoice : null;
    }

    public async Task IssueAsync(Guid orderId, Guid? supplierScope, CancellationToken ct = default)
    {
        var order = await _orders.GetAsync(orderId, ct);
        if (order is null || (supplierScope is not null && order.SupplierId != supplierScope))
            throw new InvalidOperationException("Order not found.");
        if (order.Status != SupplyOrderStatus.Delivered)
            throw new InvalidOperationException("An invoice can only be issued after the order is delivered.");
        if (await _invoices.GetByOrderAsync(order.Id, ct) is not null)
            throw new InvalidOperationException("This order already has an invoice.");

        var now = DateTime.UtcNow;
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            SupplyOrderId = order.Id,
            SupplierId = order.SupplierId,
            Number = $"INV-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}",
            Amount = order.Total,
            Currency = "USD",
            IssuedAtUtc = now,
            DueAtUtc = now.AddDays(PaymentTermDays)
        };
        await _invoices.AddAsync(invoice, ct);

        // Manual orders have no Event Hub behind them, so there is nobody to notify.
        if (order.ExternalOrderId is Guid external)
        {
            _outbox.Enqueue(new InvoiceIssued(invoice.Id, external, order.SupplierId, invoice.Amount, invoice.Currency,
                invoice.IssuedAtUtc, invoice.DueAtUtc) { CorrelationId = external });
        }

        await _invoices.SaveChangesAsync(ct);
        _logger.LogInformation("Invoice {Number} issued for order {OrderId}: {Amount}", invoice.Number, order.Id, invoice.Amount);
    }

    public async Task ApplyPaymentAsync(PaymentCompleted m, CancellationToken ct = default)
    {
        var invoice = await _invoices.GetAsync(m.InvoiceId, ct)
                      ?? throw new InvalidOperationException($"Payment {m.PaymentId} names an unknown invoice {m.InvoiceId}.");

        if (invoice.Status == InvoiceStatus.Paid)
        {
            _logger.LogInformation("Invoice {Number} is already paid; payment {PaymentId} ignored", invoice.Number, m.PaymentId);
            return;
        }

        // A payment that does not match the bill is a problem for a human: refuse it so it ends in the dead-letter queue.
        if (m.Amount != invoice.Amount)
            throw new InvalidOperationException($"Payment {m.PaymentId} is {m.Amount} but invoice {invoice.Number} is {invoice.Amount}.");

        invoice.MarkPaid(m.ProviderReference);
        await _invoices.SaveChangesAsync(ct);
        _logger.LogInformation("Invoice {Number} paid ({Reference})", invoice.Number, m.ProviderReference);
        await _live.NotifySupplierAsync(invoice.SupplierId, "invoices", "Invoice paid",
            $"{invoice.Number}: {invoice.Amount:N2} {invoice.Currency} received.", "/Invoices");
    }
}
