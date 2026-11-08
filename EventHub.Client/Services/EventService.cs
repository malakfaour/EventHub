using EventHub.Client.Models;
using EventHub.Client.Repositories;
using EventHub.Client.ViewModels;
using EventHub.Contracts;
using EventHub.Messaging;

namespace EventHub.Client.Services;

/// <summary>Business rules for events. Save first, publish second: data and outbox row commit together.</summary>
public sealed class EventService : IEventService
{
    private readonly IEventRepository _events;
    private readonly ICategoryRepository _categories;
    private readonly IOutbox _outbox;
    private readonly ILogger<EventService> _logger;

    public EventService(IEventRepository events, ICategoryRepository categories, IOutbox outbox, ILogger<EventService> logger)
    {
        _events = events;
        _categories = categories;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<Guid> CreateAndSubmitAsync(CreateEventViewModel vm, Guid organizerId, CancellationToken ct = default)
    {
        var startsAtUtc = DateTime.SpecifyKind(vm.StartsAt, DateTimeKind.Utc);
        var ev = Event.CreateDraft(organizerId, vm.Title.Trim(), vm.Description.Trim(), vm.CategoryId,
            startsAtUtc, vm.Location.Trim(), vm.Capacity, vm.TicketPrice);
        ev.Submit(); // Draft -> Pending

        await _events.AddAsync(ev, ct);

        // CorrelationId = event id, so the whole life of one event can be traced in both apps' logs.
        _outbox.Enqueue(new EventSubmitted(ev.Id, ev.OrganizerId, ev.Title, ev.Description, ev.CategoryId,
            ev.StartsAtUtc, ev.Location, ev.Capacity, ev.TicketPrice) { CorrelationId = ev.Id });

        await _events.SaveChangesAsync(ct); // one transaction: event row + outbox row
        _logger.LogInformation("Event {EventId} saved as Pending and queued for moderation", ev.Id);
        return ev.Id;
    }

    public Task<List<Event>> GetMyEventsAsync(Guid organizerId, CancellationToken ct = default) =>
        _events.GetByOrganizerAsync(organizerId, ct);

    public Task<List<Event>> GetPublishedAsync(CancellationToken ct = default) => _events.GetPublishedAsync(ct);

    public Task<List<Category>> GetCategoriesAsync(CancellationToken ct = default) => _categories.GetActiveAsync(ct);

    public async Task ApplyApprovedAsync(EventApproved message, CancellationToken ct = default)
    {
        var ev = await _events.GetAsync(message.EventId, ct)
                 ?? throw new InvalidOperationException($"Event {message.EventId} not found for approval.");

        if (ev.Status == EventStatus.Published) return; // already applied

        ev.Approve();
        await _events.SaveChangesAsync(ct);
        _logger.LogInformation("Event {EventId} approved by {Moderator}; now Published", ev.Id, message.ModeratorId);
    }

    public async Task ApplyRejectedAsync(EventRejected message, CancellationToken ct = default)
    {
        var ev = await _events.GetAsync(message.EventId, ct)
                 ?? throw new InvalidOperationException($"Event {message.EventId} not found for rejection.");

        if (ev.Status == EventStatus.Rejected) return;

        ev.Reject(message.ReasonText);
        await _events.SaveChangesAsync(ct);
        _logger.LogInformation("Event {EventId} rejected by {Moderator}: {Reason}", ev.Id, message.ModeratorId, message.ReasonText);
    }
}
