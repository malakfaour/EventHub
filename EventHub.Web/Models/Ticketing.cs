namespace EventHub.Web.Models;

/// <summary>A kind of ticket for an event (for example Early bird, Standard, VIP) with its own price and stock.</summary>
public class TicketType
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public int Sold { get; set; }

    /// <summary>Concurrency token: two buyers taking the last ticket at once cannot both succeed.</summary>
    public byte[] RowVersion { get; set; } = [];

    public int Remaining => Quantity - Sold;
}

public enum PurchaseStatus { Paid, Failed, Refunded }

/// <summary>One checkout by a client. Failed attempts are kept too, so the admin can report on them.</summary>
public class TicketPurchase
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Event? Event { get; set; }
    public Guid ClientId { get; set; }
    public Guid TicketTypeId { get; set; }
    public string TicketTypeName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total { get; set; }
    public PurchaseStatus Status { get; set; }
    public string? PaymentReference { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<Ticket> Tickets { get; set; } = [];

    public void Refund()
    {
        if (Status == PurchaseStatus.Paid) Status = PurchaseStatus.Refunded;
    }
}

/// <summary>One admission. The code is what the QR code carries and what is checked at the door.</summary>
public class Ticket
{
    public Guid Id { get; set; }
    public Guid PurchaseId { get; set; }
    public Guid EventId { get; set; }
    public string Code { get; set; } = "";
    public string HolderName { get; set; } = "";
    public DateTime? UsedAtUtc { get; set; }

    public bool IsUsed => UsedAtUtc is not null;

    /// <summary>Marks the ticket as used at the door. A ticket can only be used once.</summary>
    public void CheckIn()
    {
        if (IsUsed) throw new InvalidOperationException($"Already used at {UsedAtUtc:HH:mm} UTC.");
        UsedAtUtc = DateTime.UtcNow;
    }
}
