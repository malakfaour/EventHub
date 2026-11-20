using EventHub.Contracts;

namespace EventHub.Web.Models;

/// <summary>A supplier as known from its published catalog. This is a local copy; the Supplier System owns the original.</summary>
public class CatalogSupplier
{
    public Guid Id { get; set; }          // the Supplier System's supplier id (not generated here)
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; }
    public List<CatalogProduct> Products { get; set; } = [];
}

public class CatalogProduct
{
    public Guid Id { get; set; }          // the supplier's product id (not generated here)
    public Guid SupplierId { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
}

/// <summary>
/// An order the organizer sent to one supplier. Its Id is the OrderId carried in the messages.
/// Status updates arrive from the supplier and can only move forward.
/// </summary>
public class SupplyOrder
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Event? Event { get; set; }
    public Guid OrganizerId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = "";
    public string Category { get; set; } = "";
    public SupplyOrderStatus Status { get; private set; } = SupplyOrderStatus.Pending;
    public DateTime DeliveryAtUtc { get; set; }
    public string DeliveryAddress { get; set; } = "";
    public string? Notes { get; set; }
    public decimal Total { get; set; }
    public string? StatusNote { get; private set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;
    public List<SupplyOrderItem> Items { get; set; } = [];

    public bool CanBeCancelled =>
        Status is SupplyOrderStatus.Pending or SupplyOrderStatus.Received or SupplyOrderStatus.Accepted or SupplyOrderStatus.Preparing;

    /// <summary>True when the order still counts towards covering an event requirement.</summary>
    public bool IsActive => Status is not (SupplyOrderStatus.Rejected or SupplyOrderStatus.Cancelled);

    private static int Rank(SupplyOrderStatus s) => s switch
    {
        SupplyOrderStatus.Pending => 0,
        SupplyOrderStatus.Received => 1,
        SupplyOrderStatus.Accepted => 2,
        SupplyOrderStatus.Preparing => 3,
        SupplyOrderStatus.Dispatched => 4,
        SupplyOrderStatus.Delivered => 5,
        _ => -1
    };

    /// <summary>Applies a status from the supplier. Returns false when it is old, duplicated or arrives after a final state.</summary>
    public bool ApplyStatus(SupplyOrderStatus status, string? note)
    {
        if (Rank(Status) < 0 || Status == SupplyOrderStatus.Delivered) return false;   // final already
        if (Rank(status) <= Rank(Status)) return false;                                // not forward
        Status = status;
        StatusNote = note;
        UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }

    public bool ApplyRejected(string reason)
    {
        if (Status is not (SupplyOrderStatus.Pending or SupplyOrderStatus.Received)) return false;
        Status = SupplyOrderStatus.Rejected;
        StatusNote = reason;
        UpdatedAtUtc = DateTime.UtcNow;
        return true;
    }

    public void Cancel(string reason)
    {
        if (!CanBeCancelled) throw new InvalidOperationException($"An order that is {Status} can no longer be cancelled.");
        Status = SupplyOrderStatus.Cancelled;
        StatusNote = reason;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public class SupplyOrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;
}
