namespace EventHub.Contracts;

/// <summary>
/// Life of one supply order. Pending lives only in the Event Hub (sent, no answer yet) and Received only in the
/// Supplier System (arrived, not decided). Everything after that is shared through OrderStatusChanged.
/// </summary>
public enum SupplyOrderStatus
{
    Pending,
    Received,
    Accepted,
    Rejected,
    Preparing,
    Dispatched,
    Delivered,
    Cancelled
}
