using EventHub.Contracts;
using EventHub.Messaging;
using SupplierSystem.Web.Models;
using SupplierSystem.Web.Repositories;
using SupplierSystem.Web.ViewModels;

namespace SupplierSystem.Web.Services;

/// <summary>
/// Order rules. A decision, the stock change and the outgoing message are saved by ONE SaveChanges, so the
/// outbox row can never exist without the decision (or the other way round).
/// </summary>
public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _orders;
    private readonly ICatalogRepository _catalog;
    private readonly IOutbox _outbox;
    private readonly ILogger<OrderService> _logger;
    private readonly IRealtimeNotifier _live;

    public OrderService(IOrderRepository orders, ICatalogRepository catalog, IOutbox outbox, ILogger<OrderService> logger,
        IRealtimeNotifier live)
    {
        _live = live;
        _orders = orders;
        _catalog = catalog;
        _outbox = outbox;
        _logger = logger;
    }

    // ───────────── Messages from the Event Hub ─────────────

    public async Task ReceiveAsync(SupplyOrderPlaced m, CancellationToken ct = default)
    {
        if (await _orders.ExistsByExternalIdAsync(m.OrderId, ct)) return; // already received

        var supplier = await _catalog.GetSupplierAsync(m.SupplierId, ct)
                       ?? throw new InvalidOperationException($"Order {m.OrderId} names an unknown supplier {m.SupplierId}.");
        if (!string.Equals(supplier.Category, m.Category, StringComparison.Ordinal))
            throw new InvalidOperationException($"Order {m.OrderId} is for {m.Category} but {supplier.Name} sells {supplier.Category}.");

        var customer = await _orders.GetCustomerByExternalIdAsync(m.OrganizerId, ct);
        if (customer is null)
        {
            customer = new Customer
            {
                Id = Guid.NewGuid(),
                ExternalOrganizerId = m.OrganizerId,
                Name = m.OrganizerName,
                Email = m.OrganizerEmail
            };
            await _orders.AddCustomerAsync(customer, ct);
        }

        await _orders.AddAsync(SupplyOrder.FromMessage(m, customer.Id), ct);
        await _orders.SaveChangesAsync(ct);
        _logger.LogInformation("Received order {OrderId} for {Event} from {Organizer}", m.OrderId, m.EventName, m.OrganizerName);
        await _live.NotifySupplierAsync(m.SupplierId, "orders", "New order received",
            $"{m.OrganizerName} ordered for {m.EventName}.", "/Orders");
    }

    public async Task ApplyCancelledAsync(OrderCancelled m, CancellationToken ct = default)
    {
        var order = await _orders.GetByExternalIdAsync(m.OrderId, ct);
        if (order is null)
        {
            _logger.LogWarning("Cancellation for unknown order {OrderId} ignored", m.OrderId);
            return;
        }

        if (!order.CanBeCancelled)
        {
            _logger.LogInformation("Order {OrderId} is {Status} and can no longer be cancelled", m.OrderId, order.Status);
            return;
        }

        var holdsStock = order.HoldsStock;
        order.Cancel(m.Reason);

        if (holdsStock)
        {
            var products = await LoadProductsAsync(order, ct);
            var before = Snapshot(products);
            foreach (var (productId, quantity) in Needed(order))
                products[productId].Inventory!.ReservedQuantity -= quantity;
            EnqueueAvailabilityChanges(order.SupplierId, products, before);
        }

        await _orders.SaveChangesAsync(ct);
        await _live.NotifySupplierAsync(order.SupplierId, "orders", "Order cancelled",
            $"{order.EventName}: {m.Reason}", $"/Orders/Details/{order.Id}");
    }

    // ───────────── Staff actions ─────────────

    public Task<List<SupplyOrder>> ListAsync(Guid? supplierScope, SupplyOrderStatus? status, CancellationToken ct = default) =>
        _orders.ListAsync(supplierScope, status, ct);

    public async Task<SupplyOrder?> GetAsync(Guid id, Guid? supplierScope, CancellationToken ct = default)
    {
        var order = await _orders.GetAsync(id, ct);
        return order is not null && InScope(order, supplierScope) ? order : null;
    }

    public async Task AcceptAsync(Guid id, Guid? supplierScope, CancellationToken ct = default)
    {
        var order = await LoadAsync(id, supplierScope, ct);
        var products = await LoadProductsAsync(order, ct);

        // Check every product first, so a half-reserved order is impossible.
        foreach (var (productId, quantity) in Needed(order))
        {
            if (!products.TryGetValue(productId, out var product) || product.Inventory is null || product.Inventory.Available < quantity)
            {
                var name = order.Lines.First(l => l.ProductId == productId).Name;
                throw new InvalidOperationException($"Not enough stock for {name}.");
            }
        }

        var before = Snapshot(products);
        order.Accept();
        foreach (var (productId, quantity) in Needed(order))
            products[productId].Inventory!.ReservedQuantity += quantity;

        EnqueueStatus(order, SupplyOrderStatus.Accepted, null);
        EnqueueAvailabilityChanges(order.SupplierId, products, before);

        await _orders.SaveChangesAsync(ct);
        _logger.LogInformation("Order {OrderId} accepted", order.Id);
    }

    public async Task RejectAsync(Guid id, Guid? supplierScope, string reasonCode, string reasonText, CancellationToken ct = default)
    {
        var order = await LoadAsync(id, supplierScope, ct);
        order.Reject(reasonText);

        if (order.ExternalOrderId is Guid external)
        {
            _outbox.Enqueue(new OrderRejected(external, order.SupplierId, reasonCode, reasonText) { CorrelationId = external });
        }

        await _orders.SaveChangesAsync(ct);
        _logger.LogInformation("Order {OrderId} rejected: {Reason}", order.Id, reasonCode);
    }

    public async Task AdvanceAsync(Guid id, Guid? supplierScope, SupplyOrderStatus next, CancellationToken ct = default)
    {
        var order = await LoadAsync(id, supplierScope, ct);

        switch (next)
        {
            case SupplyOrderStatus.Preparing:
                order.StartPreparing();
                break;

            case SupplyOrderStatus.Dispatched:
                order.Dispatch();
                var products = await LoadProductsAsync(order, ct);
                var before = Snapshot(products);
                foreach (var (productId, quantity) in Needed(order))
                {
                    var stock = products[productId].Inventory!;
                    stock.ReservedQuantity -= quantity;   // no longer promised ...
                    stock.QuantityOnHand -= quantity;     // ... because it left the warehouse
                }
                EnqueueAvailabilityChanges(order.SupplierId, products, before);
                break;

            case SupplyOrderStatus.Delivered:
                order.Deliver();
                break;

            default:
                throw new InvalidOperationException($"An order cannot be moved to {next} from here.");
        }

        EnqueueStatus(order, next, null);
        await _orders.SaveChangesAsync(ct);
        _logger.LogInformation("Order {OrderId} is now {Status}", order.Id, next);
    }

    public async Task<Guid> CreateManualAsync(Guid supplierId, ManualOrderViewModel vm, CancellationToken ct = default)
    {
        var supplier = await _catalog.GetSupplierAsync(supplierId, ct) ?? throw new InvalidOperationException("Supplier not found.");

        // Only this supplier's active products; the same product on two rows is added up.
        var wanted = vm.Items.Where(i => i.Quantity > 0).GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        if (wanted.Count == 0) throw new InvalidOperationException("Choose at least one item.");

        var products = (await _catalog.GetProductsAsync(wanted.Keys.ToList(), ct))
            .Where(p => p.SupplierId == supplier.Id && p.IsActive).ToDictionary(p => p.Id);
        if (products.Count != wanted.Count) throw new InvalidOperationException("One of the items is not in your catalog.");

        // A walk-in customer has no Event Hub account, so it gets its own customer record with a made-up external id.
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            ExternalOrganizerId = Guid.NewGuid(),
            Name = vm.CustomerName.Trim(),
            Email = vm.CustomerEmail?.Trim() ?? ""
        };
        await _orders.AddCustomerAsync(customer, ct);

        var order = SupplyOrder.CreateManual(supplier.Id, customer.Id, vm.EventName.Trim(), vm.DeliveryAtUtc,
            vm.DeliveryAddress.Trim(), vm.Notes?.Trim(),
            wanted.Select(w => (w.Key, products[w.Key].Name, products[w.Key].Unit, w.Value, products[w.Key].Price)));
        await _orders.AddAsync(order, ct);
        await _orders.SaveChangesAsync(ct);
        _logger.LogInformation("Manual order {OrderId} recorded for {Customer}", order.Id, customer.Name);
        return order.Id;
    }

    // ───────────── Helpers ─────────────

    private async Task<SupplyOrder> LoadAsync(Guid id, Guid? supplierScope, CancellationToken ct)
    {
        var order = await _orders.GetAsync(id, ct);
        if (order is null || !InScope(order, supplierScope))
            throw new InvalidOperationException("Order not found.");
        return order;
    }

    private static bool InScope(SupplyOrder order, Guid? supplierScope) =>
        supplierScope is null || order.SupplierId == supplierScope;

    /// <summary>Total quantity per product (the same product on two lines counts once).</summary>
    private static Dictionary<Guid, int> Needed(SupplyOrder order) =>
        order.Lines.GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

    private async Task<Dictionary<Guid, Product>> LoadProductsAsync(SupplyOrder order, CancellationToken ct)
    {
        var ids = order.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await _catalog.GetProductsAsync(ids, ct);
        return products.ToDictionary(p => p.Id);
    }

    private static bool IsAvailable(Product p) => p.IsActive && (p.Inventory?.Available ?? 0) > 0;

    private static Dictionary<Guid, bool> Snapshot(Dictionary<Guid, Product> products) =>
        products.ToDictionary(kv => kv.Key, kv => IsAvailable(kv.Value));

    private void EnqueueAvailabilityChanges(Guid supplierId, Dictionary<Guid, Product> products, Dictionary<Guid, bool> before)
    {
        foreach (var (productId, product) in products)
        {
            var now = IsAvailable(product);
            if (before[productId] != now)
                _outbox.Enqueue(new ItemAvailabilityChanged(supplierId, productId, now) { CorrelationId = supplierId });
        }
    }

    private void EnqueueStatus(SupplyOrder order, SupplyOrderStatus status, string? note)
    {
        if (order.ExternalOrderId is not Guid external) return; // manual orders have nobody to notify
        _outbox.Enqueue(new OrderStatusChanged(external, order.SupplierId, status, note) { CorrelationId = external });
    }
}
