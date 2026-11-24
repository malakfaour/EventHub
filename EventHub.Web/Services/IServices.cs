using EventHub.Contracts;
using EventHub.Web.Models;
using EventHub.Web.ViewModels;

namespace EventHub.Web.Services;

public interface IEventService
{
    Task<List<Event>> ListAsync(Guid organizerId, CancellationToken ct = default);
    Task<Event?> GetAsync(Guid id, Guid organizerId, CancellationToken ct = default);
    Task<Guid> CreateAsync(Guid organizerId, EventFormViewModel vm, CancellationToken ct = default);
    Task UpdateAsync(Guid id, Guid organizerId, EventFormViewModel vm, CancellationToken ct = default);
    Task AddRequirementAsync(Guid id, Guid organizerId, RequirementViewModel vm, CancellationToken ct = default);
    Task RemoveRequirementAsync(Guid id, Guid organizerId, Guid requirementId, CancellationToken ct = default);
    Task PlanAsync(Guid id, Guid organizerId, CancellationToken ct = default);
    Task CompleteAsync(Guid id, Guid organizerId, CancellationToken ct = default);
    Task AddTicketTypeAsync(Guid id, Guid organizerId, TicketTypeViewModel vm, CancellationToken ct = default);
    Task RemoveTicketTypeAsync(Guid id, Guid organizerId, Guid ticketTypeId, CancellationToken ct = default);
    Task PublishAsync(Guid id, Guid organizerId, CancellationToken ct = default);
    Task UnpublishAsync(Guid id, Guid organizerId, CancellationToken ct = default);

    /// <summary>Cancels the event and every order that can still be cancelled, in one transaction.</summary>
    Task CancelAsync(Guid id, Guid organizerId, CancellationToken ct = default);
}

public interface IOrderService
{
    Task<List<SupplyOrder>> ListAsync(Guid organizerId, CancellationToken ct = default);
    Task<List<SupplyOrder>> ListForEventAsync(Guid eventId, Guid organizerId, CancellationToken ct = default);
    Task<SupplyOrder?> GetAsync(Guid id, Guid organizerId, CancellationToken ct = default);
    Task<Guid> PlaceAsync(AppUser organizer, PlaceOrderViewModel vm, CancellationToken ct = default);
    Task CancelAsync(Guid id, Guid organizerId, string reason, CancellationToken ct = default);
    /// <summary>Admin override: same cancel path, without the organizer ownership check.</summary>
    Task CancelAsAdminAsync(Guid id, string reason, CancellationToken ct = default);

    // From the Supplier System (consumers)
    Task ApplyStatusAsync(OrderStatusChanged message, CancellationToken ct = default);
    Task ApplyRejectedAsync(OrderRejected message, CancellationToken ct = default);
}

public interface ICatalogService
{
    Task<List<CatalogSupplier>> ListSuppliersAsync(string? category, CancellationToken ct = default);
    Task<CatalogSupplier?> GetSupplierAsync(Guid id, CancellationToken ct = default);
    Task ApplyPublishedAsync(CatalogPublished message, CancellationToken ct = default);
    Task ApplyAvailabilityAsync(ItemAvailabilityChanged message, CancellationToken ct = default);
}

public interface ITicketService
{
    Task<List<Event>> ListPublicAsync(DiscoverQuery query, CancellationToken ct = default);
    Task<PublicStats> GetPublicStatsAsync(CancellationToken ct = default);
    Task<Event?> GetPublicAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>Reserves the tickets, charges the card, and issues the tickets. Throws InvalidOperationException with a readable reason.</summary>
    Task<Guid> PurchaseAsync(Guid clientId, string clientName, CheckoutViewModel vm, CancellationToken ct = default);

    Task<List<TicketPurchase>> ListMineAsync(Guid clientId, CancellationToken ct = default);
    Task<TicketPurchase?> GetMineAsync(Guid purchaseId, Guid clientId, CancellationToken ct = default);
}
