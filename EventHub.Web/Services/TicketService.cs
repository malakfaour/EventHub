using System.Security.Cryptography;
using EventHub.Web.Models;
using EventHub.Web.Repositories;
using EventHub.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Web.Services;

/// <summary>Ticket sales to clients. This stays inside the Event Hub: suppliers never hear about it.</summary>
public sealed class TicketService : ITicketService
{
    private readonly IEventRepository _events;
    private readonly ITicketRepository _tickets;
    private readonly IPaymentGateway _gateway;
    private readonly ILogger<TicketService> _logger;

    public TicketService(IEventRepository events, ITicketRepository tickets, IPaymentGateway gateway, ILogger<TicketService> logger)
    {
        _events = events;
        _tickets = tickets;
        _gateway = gateway;
        _logger = logger;
    }

    public Task<List<Event>> ListPublicAsync(DiscoverQuery query, CancellationToken ct = default) =>
        _events.ListPublishedAsync(query, ct);

    public Task<PublicStats> GetPublicStatsAsync(CancellationToken ct = default) =>
        _events.GetPublicStatsAsync(ct);

    public Task<Event?> GetPublicAsync(Guid eventId, CancellationToken ct = default) =>
        _events.GetPublicAsync(eventId, ct);

    public async Task<Guid> PurchaseAsync(Guid clientId, string clientName, CheckoutViewModel vm, CancellationToken ct = default)
    {
        var ev = await _events.GetPublicAsync(vm.EventId, ct);
        if (ev is null || !ev.IsOpen || ev.StartsAtUtc <= DateTime.UtcNow)
            throw new InvalidOperationException("This event is no longer on sale.");

        var type = ev.TicketTypes.FirstOrDefault(t => t.Id == vm.TicketTypeId)
                   ?? throw new InvalidOperationException("That ticket type does not exist.");
        if (vm.Quantity is < 1 or > 10) throw new InvalidOperationException("You can buy between 1 and 10 tickets at a time.");
        if (type.Remaining < vm.Quantity)
            throw new InvalidOperationException(type.Remaining == 0 ? "Sorry, this ticket type is sold out." : $"Only {type.Remaining} ticket(s) left.");

        var total = type.Price * vm.Quantity;

        // 1. Reserve first. The row version makes this fail if someone else took tickets at the same moment.
        type.Sold += vm.Quantity;
        try
        {
            await _tickets.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("Someone just bought tickets at the same time. Please try again.");
        }

        // 2. Charge. With a free ticket there is nothing to charge.
        var payment = total == 0
            ? new PaymentResult(true, "FREE", null)
            : await _gateway.ChargeAsync(new PaymentRequest(total, "USD", vm.CardHolder, vm.CardNumber, vm.Expiry, vm.Cvv,
                $"{vm.Quantity} x {type.Name} - {ev.Name}"), ct);

        var purchase = new TicketPurchase
        {
            Id = Guid.NewGuid(), EventId = ev.Id, ClientId = clientId,
            TicketTypeId = type.Id, TicketTypeName = type.Name, Quantity = vm.Quantity,
            UnitPrice = type.Price, Total = total, PaymentReference = payment.Reference
        };

        if (!payment.Succeeded)
        {
            // 3a. Declined: give the tickets back and keep a record of the attempt.
            type.Sold -= vm.Quantity;
            purchase.Status = PurchaseStatus.Failed;
            purchase.FailureReason = payment.Error;
            await _tickets.AddPurchaseAsync(purchase, ct);
            await _tickets.SaveChangesAsync(ct);
            throw new InvalidOperationException(payment.Error ?? "The payment failed.");
        }

        // 3b. Paid: issue the tickets.
        purchase.Status = PurchaseStatus.Paid;
        for (var i = 0; i < vm.Quantity; i++)
        {
            purchase.Tickets.Add(new Ticket
            {
                Id = Guid.NewGuid(), PurchaseId = purchase.Id, EventId = ev.Id,
                Code = NewCode(), HolderName = clientName
            });
        }
        await _tickets.AddPurchaseAsync(purchase, ct);
        await _tickets.SaveChangesAsync(ct);

        _logger.LogInformation("Client {Client} bought {Quantity} x {Type} for {Event} ({Reference})", clientId, vm.Quantity, type.Name, ev.Name, payment.Reference);
        return purchase.Id;
    }

    public Task<List<TicketPurchase>> ListMineAsync(Guid clientId, CancellationToken ct = default) =>
        _tickets.ListByClientAsync(clientId, ct);

    public async Task<TicketPurchase?> GetMineAsync(Guid purchaseId, Guid clientId, CancellationToken ct = default)
    {
        var purchase = await _tickets.GetAsync(purchaseId, ct);
        return purchase is not null && purchase.ClientId == clientId ? purchase : null;
    }

    /// <summary>Random, hard to guess code such as EH-7F3A9-C21B4.</summary>
    private static string NewCode()
    {
        var hex = Convert.ToHexString(RandomNumberGenerator.GetBytes(5));
        return $"EH-{hex[..5]}-{hex[5..]}";
    }
}
