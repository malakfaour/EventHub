namespace EventHub.Contracts;

public readonly record struct MessageRoute(string Exchange, string RoutingKey);

/// <summary>Maps each message type to its exchange and routing key, so callers just publish the object.</summary>
public static class MessageRoutes
{
    private static readonly Dictionary<Type, MessageRoute> Map = new()
    {
        [typeof(EventSubmitted)] = new(Topology.ClientExchange, Topology.Keys.EventSubmitted),
        [typeof(EventUpdated)] = new(Topology.ClientExchange, Topology.Keys.EventUpdated),
        [typeof(EventCancelled)] = new(Topology.ClientExchange, Topology.Keys.EventCancelled),
        [typeof(EventReported)] = new(Topology.ClientExchange, Topology.Keys.EventReported),
        [typeof(RsvpCreated)] = new(Topology.ClientExchange, Topology.Keys.RsvpCreated),
        [typeof(RsvpCancelled)] = new(Topology.ClientExchange, Topology.Keys.RsvpCancelled),
        [typeof(CommentPosted)] = new(Topology.ClientExchange, Topology.Keys.CommentPosted),
        [typeof(UserRegistered)] = new(Topology.ClientExchange, Topology.Keys.UserRegistered),
        [typeof(PaymentCompleted)] = new(Topology.ClientExchange, Topology.Keys.PaymentCompleted),

        [typeof(EventApproved)] = new(Topology.AdminExchange, Topology.Keys.EventApproved),
        [typeof(EventRejected)] = new(Topology.AdminExchange, Topology.Keys.EventRejected),
        [typeof(ReportResolved)] = new(Topology.AdminExchange, Topology.Keys.ReportResolved),
        [typeof(UserBanned)] = new(Topology.AdminExchange, Topology.Keys.UserBanned),
        [typeof(UserUnbanned)] = new(Topology.AdminExchange, Topology.Keys.UserUnbanned),
        [typeof(CategoryChanged)] = new(Topology.AdminExchange, Topology.Keys.CategoryChanged),
    };

    public static MessageRoute For<T>() where T : MessageBase => For(typeof(T));

    public static MessageRoute For(Type type) =>
        Map.TryGetValue(type, out var route)
            ? route
            : throw new InvalidOperationException($"No route registered for message type {type.Name}.");

    /// <summary>Finds a message type by its short name (used by the outbox, which stores the name as text).</summary>
    public static Type? Resolve(string typeName) => Map.Keys.FirstOrDefault(t => t.Name == typeName);

    public static IReadOnlyCollection<Type> AllTypes => Map.Keys;
}
