namespace EventHub.Contracts;

// ───────────── Shared parts ─────────────

/// <summary>One line of a supply order.</summary>
public sealed record OrderLine(Guid ProductId, string Name, string Unit, int Quantity, decimal UnitPrice);

/// <summary>One product in a supplier's catalog.</summary>
public sealed record CatalogItem(Guid ProductId, string Name, string Unit, decimal Price, bool IsAvailable);

// ───────────── Event Hub  ->  Supplier System  (exchange: eventhub.exchange) ─────────────

/// <summary>An organizer ordered supplies for an event from one supplier.</summary>
public sealed record SupplyOrderPlaced(
    Guid OrderId, Guid EventId, string EventName,
    Guid OrganizerId, string OrganizerName, string OrganizerEmail,
    Guid SupplierId, string Category,
    DateTime DeliveryAtUtc, string DeliveryAddress, string? Notes,
    IReadOnlyList<OrderLine> Lines) : MessageBase, ICategorized;

/// <summary>The organizer cancelled an order before delivery.</summary>
public sealed record OrderCancelled(
    Guid OrderId, Guid SupplierId, string Category, string Reason) : MessageBase, ICategorized;

/// <summary>The organizer paid an invoice.</summary>
public sealed record PaymentCompleted(
    Guid PaymentId, Guid OrderId, Guid InvoiceId, Guid SupplierId,
    decimal Amount, string Currency, string ProviderReference) : MessageBase;

// ───────────── Supplier System  ->  Event Hub  (exchange: supplier.exchange) ─────────────

/// <summary>The supplier moved an order forward: Accepted, Preparing, Dispatched or Delivered.</summary>
public sealed record OrderStatusChanged(
    Guid OrderId, Guid SupplierId, SupplyOrderStatus Status, string? Note) : MessageBase;

/// <summary>The supplier cannot fulfil the order.</summary>
public sealed record OrderRejected(
    Guid OrderId, Guid SupplierId, string ReasonCode, string ReasonText) : MessageBase;

/// <summary>Invoice sent after delivery.</summary>
public sealed record InvoiceIssued(
    Guid InvoiceId, Guid OrderId, Guid SupplierId, decimal Amount, string Currency,
    DateTime IssuedAtUtc, DateTime DueAtUtc) : MessageBase;

/// <summary>Full snapshot of one supplier's catalog. The Event Hub replaces its local copy with it.</summary>
public sealed record CatalogPublished(
    Guid SupplierId, string SupplierName, string Category,
    IReadOnlyList<CatalogItem> Items) : MessageBase;

/// <summary>One item went out of stock or came back.</summary>
public sealed record ItemAvailabilityChanged(
    Guid SupplierId, Guid ProductId, bool IsAvailable) : MessageBase;
