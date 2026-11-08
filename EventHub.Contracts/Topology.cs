namespace EventHub.Contracts;

/// <summary>Binding of one queue to an exchange through one or more routing-key patterns.</summary>
public sealed record QueueBinding(string Exchange, string Queue, IReadOnlyList<string> RoutingKeys);

/// <summary>Single source of truth for every exchange, queue and routing key name.</summary>
public static class Topology
{
    public const string ClientExchange = "eventhub.client";      // Client -> Admin
    public const string AdminExchange = "eventhub.admin";        // Admin  -> Client
    public const string DeadLetterExchange = "eventhub.dlx";     // retry + dead letters

    public static class Queues
    {
        public const string AdminModeration = "admin.moderation";
        public const string AdminActivity = "admin.activity";
        public const string AdminAccounts = "admin.accounts";
        public const string AdminPayments = "admin.payments";
        public const string ClientModerationResults = "client.moderation-results";
        public const string ClientUsers = "client.users";
        public const string ClientCatalog = "client.catalog";
    }

    public static class Keys
    {
        public const string EventSubmitted = "event.submitted";
        public const string EventUpdated = "event.updated";
        public const string EventCancelled = "event.cancelled";
        public const string EventReported = "event.reported";
        public const string RsvpCreated = "rsvp.created";
        public const string RsvpCancelled = "rsvp.cancelled";
        public const string CommentPosted = "comment.posted";
        public const string UserRegistered = "user.registered";
        public const string PaymentCompleted = "payment.completed";

        public const string EventApproved = "event.approved";
        public const string EventRejected = "event.rejected";
        public const string ReportResolved = "report.resolved";
        public const string UserBanned = "user.banned";
        public const string UserUnbanned = "user.unbanned";
        public const string CategoryChanged = "category.changed";
    }

    public static string RetryQueue(string queue) => $"{queue}.retry";
    public static string DeadLetterQueue(string queue) => $"{queue}.dlq";

    /// <summary>Every main queue and the routing keys bound to it.</summary>
    public static readonly IReadOnlyList<QueueBinding> Bindings =
    [
        new(ClientExchange, Queues.AdminModeration,
            [Keys.EventSubmitted, Keys.EventUpdated, Keys.EventCancelled, Keys.EventReported]),
        new(ClientExchange, Queues.AdminActivity, ["rsvp.*", Keys.CommentPosted]),
        new(ClientExchange, Queues.AdminAccounts, [Keys.UserRegistered]),
        new(ClientExchange, Queues.AdminPayments, [Keys.PaymentCompleted]),

        new(AdminExchange, Queues.ClientModerationResults,
            [Keys.EventApproved, Keys.EventRejected, Keys.ReportResolved]),
        new(AdminExchange, Queues.ClientUsers, [Keys.UserBanned, Keys.UserUnbanned]),
        new(AdminExchange, Queues.ClientCatalog, [Keys.CategoryChanged]),
    ];
}
