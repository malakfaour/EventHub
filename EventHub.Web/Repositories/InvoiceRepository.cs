using EventHub.Web.Data;
using EventHub.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Web.Repositories;

public interface IInvoiceRepository
{
    Task AddAsync(Invoice invoice, CancellationToken ct = default);
    Task<Invoice?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Invoice?> GetByOrderAsync(Guid orderId, CancellationToken ct = default);
    Task<List<Invoice>> ListByOrganizerAsync(Guid organizerId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly AppDbContext _db;
    public InvoiceRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Invoice invoice, CancellationToken ct = default) => await _db.Invoices.AddAsync(invoice, ct);

    public Task<Invoice?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Invoices.Include(i => i.Order).ThenInclude(o => o!.Event).FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<Invoice?> GetByOrderAsync(Guid orderId, CancellationToken ct = default) =>
        _db.Invoices.FirstOrDefaultAsync(i => i.OrderId == orderId, ct);

    public Task<List<Invoice>> ListByOrganizerAsync(Guid organizerId, CancellationToken ct = default) =>
        _db.Invoices.AsNoTracking().Include(i => i.Order).ThenInclude(o => o!.Event)
            .Where(i => i.Order!.OrganizerId == organizerId)
            .OrderBy(i => i.Status).ThenBy(i => i.DueAtUtc).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
