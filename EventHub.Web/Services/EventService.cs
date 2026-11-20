using EventHub.Contracts;
using EventHub.Messaging;
using EventHub.Web.Models;
using EventHub.Web.Repositories;
using EventHub.Web.ViewModels;

namespace EventHub.Web.Services;

public sealed class EventService : IEventService
{
    private readonly IEventRepository _events;
    private readonly IOrderRepository _orders;
    private readonly ITicketRepository _tickets;
    private readonly IOutbox _outbox;
    private readonly ILogger<EventService> _logger;

    public EventService(IEventRepository events, IOrderRepository orders, ITicketRepository tickets, IOutbox outbox, ILogger<EventService> logger)
    {
        _events = events;
        _orders = orders;
        _tickets = tickets;
        _outbox = outbox;
        _logger = logger;
    }

    public Task<List<Event>> ListAsync(Guid organizerId, CancellationToken ct = default) =>
        _events.ListByOrganizerAsync(organizerId, ct);

    public async Task<Event?> GetAsync(Guid id, Guid organizerId, CancellationToken ct = default)
    {
        var ev = await _events.GetAsync(id, ct);
        return ev is not null && ev.OrganizerId == organizerId ? ev : null;   // never someone else's event
    }

    public async Task<Guid> CreateAsync(Guid organizerId, EventFormViewModel vm, CancellationToken ct = default)
    {
        var ev = new Event
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Name = vm.Name.Trim(),
            Description = vm.Description?.Trim(),
            Venue = vm.Venue.Trim(),
            StartsAtUtc = DateTime.SpecifyKind(vm.StartsAtUtc, DateTimeKind.Utc),
            ExpectedGuests = vm.ExpectedGuests,
            EventType = EventTypes.All.Contains(vm.EventType) ? vm.EventType : "Other",
            ImageUrl = string.IsNullOrWhiteSpace(vm.ImageUrl) ? null : vm.ImageUrl.Trim()
        };
        foreach (var category in vm.Needs.Where(SupplyCategories.IsValid).Distinct())
            ev.Requirements.Add(new EventRequirement { Id = Guid.NewGuid(), EventId = ev.Id, Category = category });

        await _events.AddAsync(ev, ct);
        await _events.SaveChangesAsync(ct);
        _logger.LogInformation("Event {Event} created by {Organizer}", ev.Name, organizerId);
        return ev.Id;
    }

    public async Task UpdateAsync(Guid id, Guid organizerId, EventFormViewModel vm, CancellationToken ct = default)
    {
        var ev = await LoadOpenAsync(id, organizerId, ct);
        ev.Name = vm.Name.Trim();
        ev.Description = vm.Description?.Trim();
        ev.Venue = vm.Venue.Trim();
        ev.StartsAtUtc = DateTime.SpecifyKind(vm.StartsAtUtc, DateTimeKind.Utc);
        ev.ExpectedGuests = vm.ExpectedGuests;
        ev.EventType = EventTypes.All.Contains(vm.EventType) ? vm.EventType : "Other";
        ev.ImageUrl = string.IsNullOrWhiteSpace(vm.ImageUrl) ? null : vm.ImageUrl.Trim();
        await _events.SaveChangesAsync(ct);
    }

    public async Task AddRequirementAsync(Guid id, Guid organizerId, RequirementViewModel vm, CancellationToken ct = default)
    {
        var ev = await LoadOpenAsync(id, organizerId, ct);
        if (!SupplyCategories.IsValid(vm.Category)) throw new InvalidOperationException("Unknown supply category.");
        if (ev.Requirements.Any(r => r.Category == vm.Category))
            throw new InvalidOperationException($"{SupplyCategories.DisplayName(vm.Category)} is already on the list.");

        await _events.AddRequirementAsync(
            new EventRequirement { Id = Guid.NewGuid(), EventId = ev.Id, Category = vm.Category, Notes = vm.Notes?.Trim() }, ct);
        await _events.SaveChangesAsync(ct);
    }

    public async Task RemoveRequirementAsync(Guid id, Guid organizerId, Guid requirementId, CancellationToken ct = default)
    {
        var ev = await LoadOpenAsync(id, organizerId, ct);
        var requirement = ev.Requirements.FirstOrDefault(r => r.Id == requirementId);
        if (requirement is null) return;

        var orders = await _orders.ListByEventAsync(ev.Id, ct);
        if (orders.Any(o => o.Category == requirement.Category && o.IsActive))
            throw new InvalidOperationException("Cancel the active order for this category first.");

        ev.Requirements.Remove(requirement);
        await _events.SaveChangesAsync(ct);
    }

    public async Task PlanAsync(Guid id, Guid organizerId, CancellationToken ct = default)
    {
        var ev = await LoadAsync(id, organizerId, ct);
        ev.Plan();
        await _events.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(Guid id, Guid organizerId, CancellationToken ct = default)
    {
        var ev = await LoadAsync(id, organizerId, ct);
        ev.Complete();
        await _events.SaveChangesAsync(ct);
    }

    public async Task AddTicketTypeAsync(Guid id, Guid organizerId, TicketTypeViewModel vm, CancellationToken ct = default)
    {
        var ev = await LoadOpenAsync(id, organizerId, ct);
        if (ev.TicketTypes.Any(t => string.Equals(t.Name, vm.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("There is already a ticket type with that name.");

        await _events.AddTicketTypeAsync(new TicketType
        {
            Id = Guid.NewGuid(), EventId = ev.Id, Name = vm.Name.Trim(),
            Description = vm.Description?.Trim(), Price = vm.Price, Quantity = vm.Quantity
        }, ct);
        await _events.SaveChangesAsync(ct);
    }

    public async Task RemoveTicketTypeAsync(Guid id, Guid organizerId, Guid ticketTypeId, CancellationToken ct = default)
    {
        var ev = await LoadOpenAsync(id, organizerId, ct);
        var type = ev.TicketTypes.FirstOrDefault(t => t.Id == ticketTypeId);
        if (type is null) return;
        if (type.Sold > 0) throw new InvalidOperationException("Tickets of this type were already sold.");
        if (ev.IsPublished && ev.TicketTypes.Count == 1) throw new InvalidOperationException("Unpublish the event before removing its last ticket type.");

        ev.TicketTypes.Remove(type);
        await _events.SaveChangesAsync(ct);
    }

    public async Task PublishAsync(Guid id, Guid organizerId, CancellationToken ct = default)
    {
        var ev = await LoadAsync(id, organizerId, ct);
        ev.Publish();
        await _events.SaveChangesAsync(ct);
    }

    public async Task UnpublishAsync(Guid id, Guid organizerId, CancellationToken ct = default)
    {
        var ev = await LoadAsync(id, organizerId, ct);
        ev.Unpublish();
        await _events.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(Guid id, Guid organizerId, CancellationToken ct = default)
    {
        var ev = await LoadAsync(id, organizerId, ct);
        ev.Cancel();

        // Orders still in flight are cancelled with the event. All changes and messages are saved together.
        foreach (var order in await _orders.ListByEventAsync(ev.Id, ct))
        {
            if (!order.CanBeCancelled) continue;
            order.Cancel("Event cancelled");
            _outbox.Enqueue(new OrderCancelled(order.Id, order.SupplierId, order.Category, "Event cancelled") { CorrelationId = order.Id });
        }

        // Tickets already sold are refunded with the event (the demo gateway has nothing to call).
        foreach (var purchase in await _tickets.ListByEventAsync(ev.Id, ct))
            purchase.Refund();

        await _events.SaveChangesAsync(ct);
        _logger.LogInformation("Event {Event} cancelled", ev.Id);
    }

    private async Task<Event> LoadAsync(Guid id, Guid organizerId, CancellationToken ct) =>
        await GetAsync(id, organizerId, ct) ?? throw new InvalidOperationException("Event not found.");

    private async Task<Event> LoadOpenAsync(Guid id, Guid organizerId, CancellationToken ct)
    {
        var ev = await LoadAsync(id, organizerId, ct);
        if (!ev.IsOpen) throw new InvalidOperationException($"A {ev.Status} event can no longer be changed.");
        return ev;
    }
}
