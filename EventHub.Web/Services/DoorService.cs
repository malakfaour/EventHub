using EventHub.Web.Models;
using EventHub.Web.Repositories;

namespace EventHub.Web.Services;

public record CheckInResult(bool Ok, string Message);

/// <summary>What the organizer does on the day: manage the guest list and check tickets in at the door.</summary>
public interface IDoorService
{
    Task<Event?> GetEventAsync(Guid eventId, Guid organizerId, CancellationToken ct = default);
    Task AddGuestAsync(Guid eventId, Guid organizerId, string name, string? email, int partySize, CancellationToken ct = default);
    Task RemoveGuestAsync(Guid eventId, Guid organizerId, Guid guestId, CancellationToken ct = default);
    Task<List<Ticket>> ListTicketsAsync(Guid eventId, Guid organizerId, CancellationToken ct = default);
    Task<CheckInResult> CheckInAsync(Guid eventId, Guid organizerId, string code, CancellationToken ct = default);
}

public sealed class DoorService : IDoorService
{
    private readonly IEventRepository _events;
    private readonly ITicketRepository _tickets;

    public DoorService(IEventRepository events, ITicketRepository tickets)
    {
        _events = events;
        _tickets = tickets;
    }

    // Every method starts here, so an organizer can never touch another organizer's event.
    public async Task<Event?> GetEventAsync(Guid eventId, Guid organizerId, CancellationToken ct = default)
    {
        var ev = await _events.GetAsync(eventId, ct);
        return ev is not null && ev.OrganizerId == organizerId ? ev : null;
    }

    private async Task<Event> OwnedAsync(Guid eventId, Guid organizerId, CancellationToken ct) =>
        await GetEventAsync(eventId, organizerId, ct) ?? throw new InvalidOperationException("Event not found.");

    public async Task AddGuestAsync(Guid eventId, Guid organizerId, string name, string? email, int partySize, CancellationToken ct = default)
    {
        var ev = await OwnedAsync(eventId, organizerId, ct);
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("A guest needs a name.");
        if (partySize < 1 || partySize > 20) throw new InvalidOperationException("Party size must be between 1 and 20.");

        // Explicit add: a new child with a preset Guid on a tracked parent would be treated as an update.
        await _events.AddGuestAsync(new Guest
        {
            Id = Guid.NewGuid(), EventId = ev.Id, Name = name.Trim(), Email = email?.Trim(), PartySize = partySize
        }, ct);
        await _events.SaveChangesAsync(ct);
    }

    public async Task RemoveGuestAsync(Guid eventId, Guid organizerId, Guid guestId, CancellationToken ct = default)
    {
        var ev = await OwnedAsync(eventId, organizerId, ct);
        var guest = ev.Guests.FirstOrDefault(g => g.Id == guestId);
        if (guest is null) return;
        ev.Guests.Remove(guest);
        await _events.SaveChangesAsync(ct);
    }

    public async Task<List<Ticket>> ListTicketsAsync(Guid eventId, Guid organizerId, CancellationToken ct = default)
    {
        await OwnedAsync(eventId, organizerId, ct);
        return await _tickets.ListTicketsAsync(eventId, ct);
    }

    public async Task<CheckInResult> CheckInAsync(Guid eventId, Guid organizerId, string code, CancellationToken ct = default)
    {
        await OwnedAsync(eventId, organizerId, ct);
        var ticket = await _tickets.GetTicketByCodeAsync(eventId, (code ?? "").Trim(), ct);
        if (ticket is null) return new CheckInResult(false, "No ticket with that code for this event.");
        if (!await _tickets.IsPaidAsync(ticket.PurchaseId, ct)) return new CheckInResult(false, "This ticket was refunded and is no longer valid.");

        try
        {
            ticket.CheckIn();
            await _tickets.SaveChangesAsync(ct);
            return new CheckInResult(true, $"Welcome, {ticket.HolderName}!");
        }
        catch (InvalidOperationException ex)
        {
            return new CheckInResult(false, $"{ticket.HolderName}: {ex.Message}");
        }
    }
}
