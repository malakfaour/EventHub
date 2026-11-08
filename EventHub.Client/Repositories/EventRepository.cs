using EventHub.Client.Data;
using EventHub.Client.Models;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Client.Repositories;

/// <summary>EF Core data access. Repositories hold queries; business rules live in the services.</summary>
public sealed class EventRepository : IEventRepository
{
    private readonly AppDbContext _db;

    public EventRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Event ev, CancellationToken ct = default) => await _db.Events.AddAsync(ev, ct);

    public Task<Event?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Events.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<List<Event>> GetByOrganizerAsync(Guid organizerId, CancellationToken ct = default) =>
        _db.Events.AsNoTracking().Where(e => e.OrganizerId == organizerId)
            .OrderByDescending(e => e.CreatedAtUtc).ToListAsync(ct);

    public Task<List<Event>> GetPublishedAsync(CancellationToken ct = default) =>
        _db.Events.AsNoTracking().Where(e => e.Status == EventStatus.Published && e.StartsAtUtc >= DateTime.UtcNow)
            .OrderBy(e => e.StartsAtUtc).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _db;

    public CategoryRepository(AppDbContext db) => _db = db;

    public Task<List<Category>> GetActiveAsync(CancellationToken ct = default) =>
        _db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync(ct);
}
