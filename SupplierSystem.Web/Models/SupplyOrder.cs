using EventHub.Contracts;

namespace SupplierSystem.Web.Models;

public enum OrderSource { EventHub, Manual }

/// <summary>
/// An order received by this supplier. The status only changes through the methods below, which refuse
/// invalid transitions, so the order life stays Received -> Accepted -> Preparing -> Dispatched -> Delivered.
/// </summary>
public class SupplyOrder
{
    private SupplyOrder() { } // for EF Core

    public Guid Id { get; private set; }

    /// <summary>The Event Hub's order id. Null for orders entered manually by staff. No foreign key across apps.</summary>
    public Guid? ExternalOrderId { get; private set; }

    public Guid SupplierId { get; private set; }
    public Supplier? Supplier { get; private set; }
    public Guid CustomerId { get; private set; }
    public Customer? Customer { get; private set; }

    public string EventName { get; private set; } = "";
    public OrderSource Source { get; private set; }
    public SupplyOrderStatus Status { get; private set; } = SupplyOrderStatus.Received;
    public DateTime DeliveryAtUtc { get; private set; }
    public string DeliveryAddress { get; private set; } = "";
    public string? Notes { get; private set; }
    public decimal Total { get; private set; }
    public DateTime ReceivedAtUtc { get; private set; }
    public string? DecisionNote { get; private set; }
    public List<SupplyOrderLine> Lines { get; private set; } = [];

    public static SupplyOrder FromMessage(SupplyOrderPlaced m, Guid customerId)
    {
        var id = Guid.NewGuid();
        var lines = m.Lines.Select(l => new SupplyOrderLine
        {
            Id = Guid.NewGuid(),
            OrderId = id,
            ProductId = l.ProductId,
            Name = l.Name,
            Unit = l.Unit,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice
        }).ToList();

        return new SupplyOrder
        {
            Id = id,
            ExternalOrderId = m.OrderId,
            SupplierId = m.SupplierId,
            CustomerId = customerId,
            EventName = m.EventName,
            Source = OrderSource.EventHub,
            DeliveryAtUtc = m.DeliveryAtUtc,
            DeliveryAddress = m.DeliveryAddress,
            Notes = m.Notes,
            Lines = lines,
            Total = lines.Sum(l => l.Quantity * l.UnitPrice),
            ReceivedAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>An order entered by staff (a phone call or walk-in). It has no Event Hub counterpart, so nothing is sent.</summary>
    public static SupplyOrder CreateManual(Guid supplierId, Guid customerId, string eventName, DateTime deliveryAtUtc,
        string deliveryAddress, string? notes, IEnumerable<(Guid ProductId, string Name, string Unit, int Quantity, decimal UnitPrice)> items)
    {
        var id = Guid.NewGuid();
        var lines = items.Select(i => new SupplyOrderLine
        {
            Id = Guid.NewGuid(), OrderId = id, ProductId = i.ProductId, Name = i.Name, Unit = i.Unit,
            Quantity = i.Quantity, UnitPrice = i.UnitPrice
        }).ToList();
        if (lines.Count == 0) throw new InvalidOperationException("Choose at least one item.");

        return new SupplyOrder
        {
            Id = id,
            ExternalOrderId = null,
            SupplierId = supplierId,
            CustomerId = customerId,
            EventName = eventName,
            Source = OrderSource.Manual,
            DeliveryAtUtc = deliveryAtUtc,
            DeliveryAddress = deliveryAddress,
            Notes = notes,
            Lines = lines,
            Total = lines.Sum(l => l.Quantity * l.UnitPrice),
            ReceivedAtUtc = DateTime.UtcNow
        };
    }

    public void Accept()
    {
        Require(Status == SupplyOrderStatus.Received, "accepted");
        Status = SupplyOrderStatus.Accepted;
    }

    public void Reject(string reason)
    {
        Require(Status == SupplyOrderStatus.Received, "rejected");
        Status = SupplyOrderStatus.Rejected;
        DecisionNote = reason;
    }

    public void StartPreparing()
    {
        Require(Status == SupplyOrderStatus.Accepted, "prepared");
        Status = SupplyOrderStatus.Preparing;
    }

    public void Dispatch()
    {
        Require(Status == SupplyOrderStatus.Preparing, "dispatched");
        Status = SupplyOrderStatus.Dispatched;
    }

    public void Deliver()
    {
        Require(Status == SupplyOrderStatus.Dispatched, "delivered");
        Status = SupplyOrderStatus.Delivered;
    }

    public bool CanBeCancelled =>
        Status is SupplyOrderStatus.Received or SupplyOrderStatus.Accepted or SupplyOrderStatus.Preparing;

    /// <summary>True while stock is reserved for this order.</summary>
    public bool HoldsStock => Status is SupplyOrderStatus.Accepted or SupplyOrderStatus.Preparing;

    public void Cancel(string reason)
    {
        Require(CanBeCancelled, "cancelled");
        Status = SupplyOrderStatus.Cancelled;
        DecisionNote = reason;
    }

    private void Require(bool allowed, string action)
    {
        if (!allowed)
            throw new InvalidOperationException($"Order {Id} cannot be {action} while it is {Status}.");
    }
}

public class SupplyOrderLine
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }   // the product in this supplier's own catalog (plain column)
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    public decimal LineTotal => Quantity * UnitPrice;
}
