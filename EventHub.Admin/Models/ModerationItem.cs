using EventHub.Contracts;

namespace EventHub.Admin.Models;

public enum ModerationStatus { Pending, Approved, Rejected }

/// <summary>A submission received from the Client App through RabbitMQ, waiting for a human decision.</summary>
public class ModerationItem
{
    private ModerationItem() { } // for EF Core

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }          // same GUID as in the Client database (plain column, no FK)
    public Guid OrganizerId { get; private set; }
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public int CategoryId { get; private set; }
    public DateTime StartsAtUtc { get; private set; }
    public string Location { get; private set; } = "";
    public int Capacity { get; private set; }
    public decimal TicketPrice { get; private set; }
    public ModerationStatus Status { get; private set; } = ModerationStatus.Pending;
    public DateTime ReceivedAtUtc { get; private set; }

    public int? AiRiskScore { get; private set; }       // filled by the AI pre-screen in a later phase
    public string? AiExplanation { get; private set; }

    public Guid? DecidedBy { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }
    public string? DecisionReason { get; private set; }
    public bool IsFeatured { get; private set; }

    public static ModerationItem FromSubmission(EventSubmitted m) => new()
    {
        Id = Guid.NewGuid(),
        EventId = m.EventId,
        OrganizerId = m.OrganizerId,
        Title = m.Title,
        Description = m.Description,
        CategoryId = m.CategoryId,
        StartsAtUtc = m.StartsAtUtc,
        Location = m.Location,
        Capacity = m.Capacity,
        TicketPrice = m.TicketPrice,
        ReceivedAtUtc = DateTime.UtcNow
    };

    public void Approve(Guid moderatorId, bool featured)
    {
        RequirePending("approved");
        Status = ModerationStatus.Approved;
        DecidedBy = moderatorId;
        DecidedAtUtc = DateTime.UtcNow;
        IsFeatured = featured;
    }

    public void Reject(Guid moderatorId, string reason)
    {
        RequirePending("rejected");
        Status = ModerationStatus.Rejected;
        DecidedBy = moderatorId;
        DecidedAtUtc = DateTime.UtcNow;
        DecisionReason = reason;
    }

    private void RequirePending(string action)
    {
        if (Status != ModerationStatus.Pending)
            throw new InvalidOperationException($"Item {Id} cannot be {action}: it is already {Status}.");
    }
}
