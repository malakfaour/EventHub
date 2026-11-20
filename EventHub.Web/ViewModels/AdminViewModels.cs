using EventHub.Contracts;
using EventHub.Web.Models;

namespace EventHub.Web.ViewModels;

public record LabelValue(string Label, decimal Value);

public record AdminUserRow(Guid Id, string Email, string DisplayName, string Role, string? Organization, DateTimeOffset? LockoutEnd)
{
    public bool IsLocked => LockoutEnd > DateTimeOffset.UtcNow;
}

public record AdminEventRow(Guid Id, string Name, string Organizer, string Venue, DateTime StartsAtUtc,
    EventStatus Status, bool IsPublished, int ExpectedGuests, int Orders, int TicketsSold);

public record AdminOrderRow(Guid Id, Guid EventId, string EventName, string SupplierName, string Category,
    SupplyOrderStatus Status, decimal Total, DateTime CreatedAtUtc, DateTime UpdatedAtUtc)
{
    public bool IsActive => Status is not (SupplyOrderStatus.Rejected or SupplyOrderStatus.Cancelled);
    public bool CanBeCancelled => Status is SupplyOrderStatus.Pending or SupplyOrderStatus.Received
        or SupplyOrderStatus.Accepted or SupplyOrderStatus.Preparing;
}

public record AdminPurchaseRow(Guid Id, string EventName, string Client, string TicketType, int Quantity,
    decimal Total, PurchaseStatus Status, DateTime CreatedAtUtc);

public class MessageMonitorData
{
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public int TotalLogged { get; set; }
    public int OutboxPending { get; set; }
    public int OutboxRetrying { get; set; }
    public int Processed { get; set; }
    public List<MessageLogRow> Rows { get; set; } = [];
}

/// <summary>Everything the dashboard charts and the PDF/Excel exports are built from.</summary>
public class ReportData
{
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public int Users { get; set; }
    public int Organizers { get; set; }
    public int Clients { get; set; }
    public int Events { get; set; }
    public int PublishedEvents { get; set; }
    public int Orders { get; set; }
    public int TicketsSold { get; set; }
    public decimal TicketRevenue { get; set; }
    public decimal SupplySpend { get; set; }
    public decimal RejectionRate { get; set; }
    public decimal CancellationRate { get; set; }
    public List<LabelValue> EventsByStatus { get; set; } = [];
    public List<LabelValue> EventsByMonth { get; set; } = [];
    public List<LabelValue> OrdersByCategory { get; set; } = [];
    public List<LabelValue> SpendByCategory { get; set; } = [];
    public List<LabelValue> OrdersByStatus { get; set; } = [];
    public List<LabelValue> RevenueByEvent { get; set; } = [];
}
