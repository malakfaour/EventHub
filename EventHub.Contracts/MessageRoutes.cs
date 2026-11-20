namespace EventHub.Contracts;

public readonly record struct MessageRoute(string Exchange, string RoutingKey);

/// <summary>Maps each message type to its exchange and routing key, so callers just publish the object.</summary>
public static class MessageRoutes
{
    private static readonly Dictionary<Type, MessageRoute> Map = new()
    {
        [typeof(SupplyOrderPlaced)] = new(Topology.EventHubExchange, Topology.Keys.OrderPlaced),
        [typeof(OrderCancelled)] = new(Topology.EventHubExchange, Topology.Keys.OrderCancelled),
        [typeof(PaymentCompleted)] = new(Topology.EventHubExchange, Topology.Keys.PaymentCompleted),

        [typeof(OrderStatusChanged)] = new(Topology.SupplierExchange, Topology.Keys.OrderStatusChanged),
        [typeof(OrderRejected)] = new(Topology.SupplierExchange, Topology.Keys.OrderRejected),
        [typeof(InvoiceIssued)] = new(Topology.SupplierExchange, Topology.Keys.InvoiceIssued),
        [typeof(CatalogPublished)] = new(Topology.SupplierExchange, Topology.Keys.CatalogPublished),
        [typeof(ItemAvailabilityChanged)] = new(Topology.SupplierExchange, Topology.Keys.ItemAvailabilityChanged),
    };

    public static MessageRoute For<T>() where T : MessageBase => For(typeof(T));

    public static MessageRoute For(Type type) =>
        Map.TryGetValue(type, out var route)
            ? route
            : throw new InvalidOperationException($"No route registered for message type {type.Name}.");

    /// <summary>
    /// Route for one concrete message. Order messages append their category to the routing key
    /// (order.placed.catering), so each category's orders land in that supplier queue.
    /// </summary>
    public static MessageRoute For(MessageBase message)
    {
        var route = For(message.GetType());
        return message is ICategorized categorized
            ? route with { RoutingKey = $"{route.RoutingKey}.{categorized.Category}" }
            : route;
    }

    /// <summary>Finds a message type by its short name (used by the outbox, which stores the name as text).</summary>
    public static Type? Resolve(string typeName) => Map.Keys.FirstOrDefault(t => t.Name == typeName);

    public static IReadOnlyCollection<Type> AllTypes => Map.Keys;
}
