using EventHub.Admin.Data;
using EventHub.Admin.Models;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Admin.Repositories;

public sealed class ModerationRepository : IModerationRepository
{
    private readonly AppDbContext _db;

    public ModerationRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(ModerationItem item, CancellationToken ct = default) =>
        await _db.ModerationItems.AddAsync(item, ct);

    public Task<ModerationItem?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.ModerationItems.FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<bool> ExistsForEventAsync(Guid eventId, CancellationToken ct = default) =>
        _db.ModerationItems.AnyAsync(i => i.EventId == eventId, ct);

    public Task<List<ModerationItem>> ListAsync(ModerationStatus status, CancellationToken ct = default) =>
        _db.ModerationItems.AsNoTracking().Where(i => i.Status == status)
            .OrderByDescending(i => i.ReceivedAtUtc).ToListAsync(ct);

    public Task<Dictionary<int, string>> GetCategoryNamesAsync(CancellationToken ct = default) =>
        _db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
