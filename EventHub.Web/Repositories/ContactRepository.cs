using EventHub.Web.Data;
using EventHub.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Web.Repositories;

public interface IContactRepository
{
    Task AddAsync(ContactMessage message, CancellationToken ct = default);
    Task<ContactMessage?> GetAsync(Guid id, CancellationToken ct = default);
    /// <summary>Newest first. <paramref name="open"/>: true = still open, false = handled, null = all.</summary>
    Task<List<ContactMessage>> ListAsync(bool? open, CancellationToken ct = default);
    Task<int> CountOpenAsync(CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public sealed class ContactRepository : IContactRepository
{
    private readonly AppDbContext _db;
    public ContactRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(ContactMessage message, CancellationToken ct = default) => await _db.ContactMessages.AddAsync(message, ct);

    public Task<ContactMessage?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.ContactMessages.FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<List<ContactMessage>> ListAsync(bool? open, CancellationToken ct = default)
    {
        var q = _db.ContactMessages.AsNoTracking();
        if (open == true) q = q.Where(m => m.HandledAtUtc == null);
        if (open == false) q = q.Where(m => m.HandledAtUtc != null);
        return q.OrderByDescending(m => m.CreatedAtUtc).Take(500).ToListAsync(ct);
    }

    public Task<int> CountOpenAsync(CancellationToken ct = default) =>
        _db.ContactMessages.CountAsync(m => m.HandledAtUtc == null, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
