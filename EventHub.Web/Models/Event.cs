namespace EventHub.Web.Models;

public enum EventStatus { Draft, Planned, Completed, Cancelled }

/// <summary>An event being organized. Its status only changes through the methods below.</summary>
public class Event
{
    public Guid Id { get; set; }
    public Guid OrganizerId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Venue { get; set; } = "";
    public DateTime StartsAtUtc { get; set; }
    public int ExpectedGuests { get; set; }
    public string EventType { get; set; } = "Other";
    public string? ImageUrl { get; set; }
    public EventStatus Status { get; private set; } = EventStatus.Draft;
    public bool IsPublished { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<EventRequirement> Requirements { get; set; } = [];
    public List<Guest> Guests { get; set; } = [];
    public List<TicketType> TicketTypes { get; set; } = [];

    public bool IsOpen => Status is EventStatus.Draft or EventStatus.Planned;

    /// <summary>Makes the event visible to clients, who can then buy tickets.</summary>
    public void Publish()
    {
        if (!IsOpen) throw new InvalidOperationException($"A {Status} event cannot be published.");
        if (StartsAtUtc <= DateTime.UtcNow) throw new InvalidOperationException("The event date must be in the future.");
        if (TicketTypes.Count == 0) throw new InvalidOperationException("Add at least one ticket type first.");
        IsPublished = true;
        PublishedAtUtc = DateTime.UtcNow;
    }

    public void Unpublish() => IsPublished = false;

    public void Plan()
    {
        if (Status != EventStatus.Draft) throw new InvalidOperationException("Only a draft event can be marked as planned.");
        if (StartsAtUtc <= DateTime.UtcNow) throw new InvalidOperationException("The event date must be in the future.");
        Status = EventStatus.Planned;
    }

    public void Complete()
    {
        if (Status != EventStatus.Planned) throw new InvalidOperationException("Only a planned event can be completed.");
        Status = EventStatus.Completed;
    }

    public void Cancel()
    {
        if (!IsOpen) throw new InvalidOperationException($"A {Status} event cannot be cancelled.");
        Status = EventStatus.Cancelled;
        IsPublished = false;
    }
}

/// <summary>One thing the event needs: a supply category, with a note about what exactly.</summary>
public class EventRequirement
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Category { get; set; } = "";
    public string? Notes { get; set; }
}

public class Guest
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public int PartySize { get; set; } = 1;
}
