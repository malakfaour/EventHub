namespace EventHub.Contracts;

/// <summary>Binding of one queue to an exchange through one or more routing-key patterns.</summary>
public sealed record QueueBinding(string Exchange, string Queue, IReadOnlyList<string> RoutingKeys);

/// <summary>Single source of truth for every exchange, queue and routing key name.</summary>
public static class Topology
{
    public const string EventHubExchange = "eventhub.exchange";      // Event Hub      -> Supplier System
    public const string SupplierExchange = "supplier.exchange";      // Supplier System -> Event Hub
    public const string DeadLetterExchange = "eventhub.dlx";         // retry + dead letters

    public static class Queues
    {
        public const string SupplierPayments = "supplier.payments";
        public const string EventHubOrderUpdates = "eventhub.order-updates";
        public const string EventHubInvoices = "eventhub.invoices";
        public const string EventHubCatalog = "eventhub.catalog";

        /// <summary>One order queue per supply category, for example supplier.orders.catering.</summary>
        public static string SupplierOrders(string category) => $"supplier.orders.{category}";
    }

    public static class Keys
    {
        // Event Hub -> Supplier System. Order keys get the category appended: order.placed.catering
        public const string OrderPlaced = "order.placed";
        public const string OrderCancelled = "order.cancelled";
        public const string PaymentCompleted = "payment.completed";

        // Supplier System -> Event Hub
        public const string OrderStatusChanged = "order.status.changed";
        public const string OrderRejected = "order.rejected";
        public const string InvoiceIssued = "invoice.issued";
        public const string CatalogPublished = "catalog.published";
        public const string ItemAvailabilityChanged = "catalog.availability.changed";
    }

    public static string RetryQueue(string queue) => $"{queue}.retry";
    public static string DeadLetterQueue(string queue) => $"{queue}.dlq";

    /// <summary>Every main queue and the routing keys bound to it.</summary>
    public static readonly IReadOnlyList<QueueBinding> Bindings = BuildBindings();

    private static IReadOnlyList<QueueBinding> BuildBindings()
    {
        var bindings = new List<QueueBinding>();

        foreach (var category in SupplyCategories.All)
            bindings.Add(new QueueBinding(EventHubExchange, Queues.SupplierOrders(category), [$"order.*.{category}"]));

        bindings.Add(new QueueBinding(EventHubExchange, Queues.SupplierPayments, [Keys.PaymentCompleted]));

        bindings.Add(new QueueBinding(SupplierExchange, Queues.EventHubOrderUpdates, ["order.#"]));
        bindings.Add(new QueueBinding(SupplierExchange, Queues.EventHubInvoices, [Keys.InvoiceIssued]));
        bindings.Add(new QueueBinding(SupplierExchange, Queues.EventHubCatalog, ["catalog.#"]));

        return bindings;
    }
}
