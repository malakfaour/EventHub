using EventHub.Contracts;
using EventHub.Messaging;
using EventHub.Web.Models;
using EventHub.Web.Repositories;
using EventHub.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Web.Services;

public interface IInvoiceService
{
    Task<List<Invoice>> ListAsync(Guid organizerId, CancellationToken ct = default);
    Task<Invoice?> GetAsync(Guid id, Guid organizerId, CancellationToken ct = default);
    Task<Invoice?> GetByOrderAsync(Guid orderId, Guid organizerId, CancellationToken ct = default);

    /// <summary>From the supplier: store the invoice (safe to receive twice).</summary>
    Task ApplyIssuedAsync(InvoiceIssued message, CancellationToken ct = default);

    /// <summary>Charges the card and, only if that works, marks the invoice paid and queues PaymentCompleted. Returns an error text or null.</summary>
    Task<string?> PayAsync(Guid id, Guid organizerId, PayInvoiceViewModel vm, CancellationToken ct = default);
}

public sealed class InvoiceService : IInvoiceService
{
    private readonly IInvoiceRepository _invoices;
    private readonly IOrderRepository _orders;
    private readonly IPaymentGateway _gateway;
    private readonly IOutbox _outbox;
    private readonly ILogger<InvoiceService> _logger;
    private readonly IRealtimeNotifier _live;

    public InvoiceService(IInvoiceRepository invoices, IOrderRepository orders, IPaymentGateway gateway, IOutbox outbox,
        ILogger<InvoiceService> logger, IRealtimeNotifier live)
    {
        _live = live;
        _invoices = invoices;
        _orders = orders;
        _gateway = gateway;
        _outbox = outbox;
        _logger = logger;
    }

    public Task<List<Invoice>> ListAsync(Guid organizerId, CancellationToken ct = default) =>
        _invoices.ListByOrganizerAsync(organizerId, ct);

    public async Task<Invoice?> GetAsync(Guid id, Guid organizerId, CancellationToken ct = default)
    {
        var invoice = await _invoices.GetAsync(id, ct);
        return invoice?.Order?.OrganizerId == organizerId ? invoice : null;
    }

    public async Task<Invoice?> GetByOrderAsync(Guid orderId, Guid organizerId, CancellationToken ct = default)
    {
        var invoice = await _invoices.GetByOrderAsync(orderId, ct);
        if (invoice is null) return null;
        return await GetAsync(invoice.Id, organizerId, ct);
    }

    public async Task ApplyIssuedAsync(InvoiceIssued m, CancellationToken ct = default)
    {
        if (await _invoices.GetAsync(m.InvoiceId, ct) is not null) return;   // already stored

        var order = await _orders.GetAsync(m.OrderId, ct)
                    ?? throw new InvalidOperationException($"Invoice {m.InvoiceId} names an unknown order {m.OrderId}.");
        if (order.SupplierId != m.SupplierId)
            throw new InvalidOperationException($"Invoice {m.InvoiceId} comes from a different supplier than order {m.OrderId}.");

        await _invoices.AddAsync(new Invoice
        {
            Id = m.InvoiceId, OrderId = order.Id, SupplierId = m.SupplierId,
            Amount = m.Amount, Currency = m.Currency, IssuedAtUtc = m.IssuedAtUtc, DueAtUtc = m.DueAtUtc
        }, ct);
        await _invoices.SaveChangesAsync(ct);
        _logger.LogInformation("Invoice {InvoiceId} received for order {OrderId}: {Amount}", m.InvoiceId, m.OrderId, m.Amount);
        await _live.NotifyAsync(order.OrganizerId, "invoices", $"New invoice from {order.SupplierName}",
            $"{m.Amount:N2} {m.Currency} for {order.Event?.Name}", "/Invoices");
    }

    public async Task<string?> PayAsync(Guid id, Guid organizerId, PayInvoiceViewModel vm, CancellationToken ct = default)
    {
        var invoice = await GetAsync(id, organizerId, ct) ?? throw new InvalidOperationException("Invoice not found.");
        if (invoice.Status == InvoiceStatus.Paid) return "This invoice is already paid.";

        var payment = await _gateway.ChargeAsync(new PaymentRequest(invoice.Amount, invoice.Currency, vm.CardHolder,
            vm.CardNumber, vm.Expiry, vm.Cvv, $"Invoice {invoice.Number}"), ct);
        if (!payment.Succeeded) return payment.Error ?? "The payment was refused.";

        invoice.MarkPaid(payment.Reference!);
        _outbox.Enqueue(new PaymentCompleted(Guid.NewGuid(), invoice.OrderId, invoice.Id, invoice.SupplierId,
            invoice.Amount, invoice.Currency, payment.Reference!) { CorrelationId = invoice.OrderId });
        try
        {
            await _invoices.SaveChangesAsync(ct);   // invoice status + outbox row, one transaction
        }
        catch (DbUpdateConcurrencyException)
        {
            return "This invoice was just paid in another window.";
        }

        _logger.LogInformation("Invoice {Number} paid ({Reference})", invoice.Number, payment.Reference);
        return null;
    }
}
