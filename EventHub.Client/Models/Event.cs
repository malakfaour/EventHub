namespace EventHub.Client.Models;

public enum EventStatus { Draft, Pending, Published, Rejected, Cancelled, Completed }

/// <summary>
/// Core content entity. The status can only change through the methods below, which refuse invalid
/// transitions (encapsulation + state logic inside the entity instead of loose status assignments).
/// </summary>
public class Event
{
    private Event() { } // for EF Core

    public Guid Id { get; private set; }
    public Guid OrganizerId { get; private set; }
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public int CategoryId { get; private set; }
    public DateTime StartsAtUtc { get; private set; }
    public string Location { get; private set; } = "";
    public int Capacity { get; private set; }
    public decimal TicketPrice { get; private set; }
    public EventStatus Status { get; private set; } = EventStatus.Draft;
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Concurrency token: two members cannot both take the last seat.</summary>
    public byte[] RowVersion { get; private set; } = [];

    public static Event CreateDraft(Guid organizerId, string title, string description, int categoryId,
        DateTime startsAtUtc, string location, int capacity, decimal ticketPrice) => new()
    {
        Id = Guid.NewGuid(),
        OrganizerId = organizerId,
        Title = title,
        Description = description,
        CategoryId = categoryId,
        StartsAtUtc = startsAtUtc,
        Location = location,
        Capacity = capacity,
        TicketPrice = ticketPrice,
        CreatedAtUtc = DateTime.UtcNow
    };

    /// <summary>Draft or Rejected -> Pending (waiting for the Admin App's decision).</summary>
    public void Submit()
    {
        Require(Status is EventStatus.Draft or EventStatus.Rejected, "submitted");
        Status = EventStatus.Pending;
        RejectionReason = null;
    }

    /// <summary>Pending -> Published. Called when EventApproved arrives.</summary>
    public void Approve()
    {
        Require(Status == EventStatus.Pending, "approved");
        Status = EventStatus.Published;
    }

    /// <summary>Pending -> Rejected, keeping the moderator's reason. Called when EventRejected arrives.</summary>
    public void Reject(string reason)
    {
        Require(Status == EventStatus.Pending, "rejected");
        Status = EventStatus.Rejected;
        RejectionReason = reason;
    }

    public void Cancel()
    {
        Require(Status is not (EventStatus.Cancelled or EventStatus.Completed), "cancelled");
        Status = EventStatus.Cancelled;
    }

    public void Complete()
    {
        Require(Status == EventStatus.Published, "completed");
        Status = EventStatus.Completed;
    }

    private void Require(bool allowed, string action)
    {
        if (!allowed)
            throw new InvalidOperationException($"Event {Id} cannot be {action} while it is {Status}.");
    }
}
