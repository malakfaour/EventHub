using EventHub.Contracts;
using EventHub.Messaging;
using EventHub.Web.Models;
using EventHub.Web.Repositories;
using EventHub.Web.ViewModels;

namespace EventHub.Web.Services;

public sealed class OrderService : IOrderService
{
    private readonly IOrderRepository _orders;
    private readonly IEventRepository _events;
    private readonly ICatalogRepository _catalog;
    private readonly IOutbox _outbox;
    private readonly ILogger<OrderService> _logger;
    private readonly IRealtimeNotifier _live;

    public OrderService(IOrderRepository orders, IEventRepository events, ICatalogRepository catalog,
        IOutbox outbox, ILogger<OrderService> logger, IRealtimeNotifier live)
    {
        _live = live;
        _orders = orders;
        _events = events;
        _catalog = catalog;
        _outbox = outbox;
        _logger = logger;
    }

    public Task<List<SupplyOrder>> ListAsync(Guid organizerId, CancellationToken ct = default) =>
        _orders.ListByOrganizerAsync(organizerId, ct);

    public async Task<List<SupplyOrder>> ListForEventAsync(Guid eventId, Guid organizerId, CancellationToken ct = default)
    {
        var orders = await _orders.ListByEventAsync(eventId, ct);
        return orders.Where(o => o.OrganizerId == organizerId).ToList();
    }

    public async Task<SupplyOrder?> GetAsync(Guid id, Guid organizerId, CancellationToken ct = default)
    {
        var order = await _orders.GetAsync(id, ct);
        return order is not null && order.OrganizerId == organizerId ? order : null;
    }

    public async Task<Guid> PlaceAsync(AppUser organizer, PlaceOrderViewModel vm, CancellationToken ct = default)
    {
        var ev = await _events.GetAsync(vm.EventId, ct);
        if (ev is null || ev.OrganizerId != organizer.Id) throw new InvalidOperationException("Event not found.");
        if (!ev.IsOpen) throw new InvalidOperationException("Orders can only be placed for a draft or planned event.");

        var supplier = await _catalog.GetSupplierAsync(vm.SupplierId, ct)
                       ?? throw new InvalidOperationException("Supplier not found. Wait for its catalog to arrive.");
        if (!ev.Requirements.Any(r => r.Category == supplier.Category))
            throw new InvalidOperationException($"{SupplyCategories.DisplayName(supplier.Category)} is not on this event's requirement list.");

        var existing = await _orders.ListByEventAsync(ev.Id, ct);
        if (existing.Any(o => o.Category == supplier.Category && o.IsActive))
            throw new InvalidOperationException($"There is already an active {SupplyCategories.DisplayName(supplier.Category)} order for this event.");

        // Only products of this supplier that are currently available; quantities of the same product are added up.
        var wanted = vm.Items.Where(i => i.Quantity > 0)
            .GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        if (wanted.Count == 0) throw new InvalidOperationException("Choose at least one item.");

        var orderId = Guid.NewGuid();
        var items = new List<SupplyOrderItem>();
        foreach (var (productId, quantity) in wanted)
        {
            var product = supplier.Products.FirstOrDefault(p => p.Id == productId)
                          ?? throw new InvalidOperationException("An item does not belong to this supplier.");
            if (!product.IsAvailable) throw new InvalidOperationException($"{product.Name} is not available right now.");
            items.Add(new SupplyOrderItem
            {
                Id = Guid.NewGuid(), OrderId = orderId, ProductId = product.Id,
                Name = product.Name, Unit = product.Unit, Quantity = quantity, UnitPrice = product.Price   // price is frozen here
            });
        }

        var order = new SupplyOrder
        {
            Id = orderId,
            EventId = ev.Id,
            OrganizerId = organizer.Id,
            SupplierId = supplier.Id,
            SupplierName = supplier.Name,
            Category = supplier.Category,
            DeliveryAtUtc = DateTime.SpecifyKind(vm.DeliveryAtUtc, DateTimeKind.Utc),
            DeliveryAddress = vm.DeliveryAddress.Trim(),
            Notes = vm.Notes?.Trim(),
            Items = items,
            Total = items.Sum(i => i.LineTotal)
        };

        await _orders.AddAsync(order, ct);

        // Same SaveChanges as the order itself: either both are stored or neither is.
        _outbox.Enqueue(new SupplyOrderPlaced(
            order.Id, ev.Id, ev.Name, organizer.Id, organizer.DisplayName, organizer.Email ?? "",
            supplier.Id, supplier.Category, order.DeliveryAtUtc, order.DeliveryAddress, order.Notes,
            items.Select(i => new OrderLine(i.ProductId, i.Name, i.Unit, i.Quantity, i.UnitPrice)).ToList())
        { CorrelationId = order.Id });

        await _orders.SaveChangesAsync(ct);
        _logger.LogInformation("Order {OrderId} placed with {Supplier} for {Event}", order.Id, supplier.Name, ev.Name);
        return order.Id;
    }

    public async Task CancelAsync(Guid id, Guid organizerId, string reason, CancellationToken ct = default)
    {
        var order = await GetAsync(id, organizerId, ct) ?? throw new InvalidOperationException("Order not found.");
        order.Cancel(reason);
        _outbox.Enqueue(new OrderCancelled(order.Id, order.SupplierId, order.Category, reason) { CorrelationId = order.Id });
        await _orders.SaveChangesAsync(ct);
    }

    public async Task CancelAsAdminAsync(Guid id, string reason, CancellationToken ct = default)
    {
        var order = await _orders.GetAsync(id, ct) ?? throw new InvalidOperationException("Order not found.");
        order.Cancel(reason);
        _outbox.Enqueue(new OrderCancelled(order.Id, order.SupplierId, order.Category, reason) { CorrelationId = order.Id });
        await _orders.SaveChangesAsync(ct);
        _logger.LogWarning("Order {OrderId} cancelled by administrator: {Reason}", order.Id, reason);
    }

    public async Task ApplyStatusAsync(OrderStatusChanged m, CancellationToken ct = default)
    {
        var order = await _orders.GetAsync(m.OrderId, ct);
        if (order is null)
        {
            _logger.LogWarning("Status for unknown order {OrderId} ignored", m.OrderId);
            return;
        }

        if (order.ApplyStatus(m.Status, m.Note))
        {
            await _orders.SaveChangesAsync(ct);
            await _live.NotifyAsync(order.OrganizerId, "orders", $"{order.SupplierName}: {m.Status}",
                $"Your order for {order.Event?.Name} is now {m.Status}.", $"/Orders/Details/{order.Id}");
        }
        else
            _logger.LogInformation("Status {Status} for order {OrderId} ignored (order is {Current})", m.Status, m.OrderId, order.Status);
    }

    public async Task ApplyRejectedAsync(OrderRejected m, CancellationToken ct = default)
    {
        var order = await _orders.GetAsync(m.OrderId, ct);
        if (order is null) return;
        if (order.ApplyRejected($"{m.ReasonCode}: {m.ReasonText}"))
        {
            await _orders.SaveChangesAsync(ct);
            await _live.NotifyAsync(order.OrganizerId, "orders", $"{order.SupplierName} rejected your order",
                $"{order.Event?.Name}: {m.ReasonText}", $"/Orders/Details/{order.Id}");
        }
    }
}
