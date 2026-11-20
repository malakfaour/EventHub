using EventHub.Web.Data;
using EventHub.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Web.Repositories;

public interface IEventRepository
{
    Task AddAsync(Event ev, CancellationToken ct = default);
    Task<Event?> GetAsync(Guid id, CancellationToken ct = default);   // with requirements and guests
    Task<List<Event>> ListByOrganizerAsync(Guid organizerId, CancellationToken ct = default);
    Task AddRequirementAsync(EventRequirement requirement, CancellationToken ct = default);
    Task AddTicketTypeAsync(TicketType ticketType, CancellationToken ct = default);
    Task<List<Event>> ListPublishedAsync(string? search, string? type, CancellationToken ct = default);
    Task<Event?> GetPublicAsync(Guid id, CancellationToken ct = default);   // published events only, with ticket types
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ITicketRepository
{
    Task<TicketType?> GetTypeAsync(Guid id, CancellationToken ct = default);
    Task AddPurchaseAsync(TicketPurchase purchase, CancellationToken ct = default);
    Task<List<TicketPurchase>> ListByClientAsync(Guid clientId, CancellationToken ct = default);
    Task<TicketPurchase?> GetAsync(Guid id, CancellationToken ct = default);
    Task<List<TicketPurchase>> ListByEventAsync(Guid eventId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IOrderRepository
{
    Task AddAsync(SupplyOrder order, CancellationToken ct = default);
    Task<SupplyOrder?> GetAsync(Guid id, CancellationToken ct = default);   // with items
    Task<List<SupplyOrder>> ListByEventAsync(Guid eventId, CancellationToken ct = default);
    Task<List<SupplyOrder>> ListByOrganizerAsync(Guid organizerId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface ICatalogRepository
{
    Task<CatalogSupplier?> GetSupplierAsync(Guid id, CancellationToken ct = default);   // with products
    Task<List<CatalogSupplier>> ListSuppliersAsync(string? category, CancellationToken ct = default);
    Task AddSupplierAsync(CatalogSupplier supplier, CancellationToken ct = default);
    Task<CatalogProduct?> GetProductAsync(Guid productId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public sealed class EventRepository : IEventRepository
{
    private readonly AppDbContext _db;
    public EventRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Event ev, CancellationToken ct = default) => await _db.Events.AddAsync(ev, ct);

    public Task<Event?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Events.Include(e => e.Requirements).Include(e => e.Guests).Include(e => e.TicketTypes)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<List<Event>> ListByOrganizerAsync(Guid organizerId, CancellationToken ct = default) =>
        _db.Events.AsNoTracking().Include(e => e.Requirements)
            .Where(e => e.OrganizerId == organizerId).OrderBy(e => e.StartsAtUtc).ToListAsync(ct);

    // Marked as Added explicitly: a new child with a preset Guid added to a tracked parent would otherwise be treated as an update.
    public async Task AddRequirementAsync(EventRequirement requirement, CancellationToken ct = default) =>
        await _db.Requirements.AddAsync(requirement, ct);

    public async Task AddTicketTypeAsync(TicketType ticketType, CancellationToken ct = default) =>
        await _db.TicketTypes.AddAsync(ticketType, ct);

    public Task<List<Event>> ListPublishedAsync(string? search, string? type, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var query = _db.Events.AsNoTracking().Include(e => e.TicketTypes)
            .Where(e => e.IsPublished && e.StartsAtUtc > now);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(e => e.Name.Contains(search) || e.Venue.Contains(search));
        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(e => e.EventType == type);
        return query.OrderBy(e => e.StartsAtUtc).ToListAsync(ct);
    }

    public Task<Event?> GetPublicAsync(Guid id, CancellationToken ct = default) =>
        _db.Events.Include(e => e.TicketTypes).FirstOrDefaultAsync(e => e.Id == id && e.IsPublished, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public sealed class TicketRepository : ITicketRepository
{
    private readonly AppDbContext _db;
    public TicketRepository(AppDbContext db) => _db = db;

    public Task<TicketType?> GetTypeAsync(Guid id, CancellationToken ct = default) =>
        _db.TicketTypes.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task AddPurchaseAsync(TicketPurchase purchase, CancellationToken ct = default) =>
        await _db.Purchases.AddAsync(purchase, ct);

    public Task<List<TicketPurchase>> ListByClientAsync(Guid clientId, CancellationToken ct = default) =>
        _db.Purchases.AsNoTracking().Include(p => p.Event).Include(p => p.Tickets)
            .Where(p => p.ClientId == clientId && p.Status != PurchaseStatus.Failed)
            .OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);

    public Task<TicketPurchase?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Purchases.Include(p => p.Event).Include(p => p.Tickets).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<List<TicketPurchase>> ListByEventAsync(Guid eventId, CancellationToken ct = default) =>
        _db.Purchases.Where(p => p.EventId == eventId).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public sealed class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;
    public OrderRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(SupplyOrder order, CancellationToken ct = default) => await _db.Orders.AddAsync(order, ct);

    public Task<SupplyOrder?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Orders.Include(o => o.Items).Include(o => o.Event).FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<List<SupplyOrder>> ListByEventAsync(Guid eventId, CancellationToken ct = default) =>
        _db.Orders.Include(o => o.Items).Where(o => o.EventId == eventId).OrderByDescending(o => o.CreatedAtUtc).ToListAsync(ct);

    public Task<List<SupplyOrder>> ListByOrganizerAsync(Guid organizerId, CancellationToken ct = default) =>
        _db.Orders.AsNoTracking().Include(o => o.Event)
            .Where(o => o.OrganizerId == organizerId).OrderByDescending(o => o.CreatedAtUtc).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public sealed class CatalogRepository : ICatalogRepository
{
    private readonly AppDbContext _db;
    public CatalogRepository(AppDbContext db) => _db = db;

    public Task<CatalogSupplier?> GetSupplierAsync(Guid id, CancellationToken ct = default) =>
        _db.CatalogSuppliers.Include(s => s.Products).FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<List<CatalogSupplier>> ListSuppliersAsync(string? category, CancellationToken ct = default)
    {
        var query = _db.CatalogSuppliers.AsNoTracking().Include(s => s.Products).AsQueryable();
        if (!string.IsNullOrEmpty(category)) query = query.Where(s => s.Category == category);
        return query.OrderBy(s => s.Category).ThenBy(s => s.Name).ToListAsync(ct);
    }

    public async Task AddSupplierAsync(CatalogSupplier supplier, CancellationToken ct = default) =>
        await _db.CatalogSuppliers.AddAsync(supplier, ct);

    public Task<CatalogProduct?> GetProductAsync(Guid productId, CancellationToken ct = default) =>
        _db.CatalogProducts.FirstOrDefaultAsync(p => p.Id == productId, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
