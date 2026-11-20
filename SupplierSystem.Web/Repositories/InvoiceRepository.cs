using Microsoft.EntityFrameworkCore;
using SupplierSystem.Web.Data;
using SupplierSystem.Web.Models;

namespace SupplierSystem.Web.Repositories;

public interface IInvoiceRepository
{
    Task AddAsync(Invoice invoice, CancellationToken ct = default);
    Task<Invoice?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Invoice?> GetByOrderAsync(Guid supplyOrderId, CancellationToken ct = default);
    Task<List<Invoice>> ListAsync(Guid? supplierId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly AppDbContext _db;
    public InvoiceRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(Invoice invoice, CancellationToken ct = default) => await _db.Invoices.AddAsync(invoice, ct);

    public Task<Invoice?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Invoices.Include(i => i.Order).FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<Invoice?> GetByOrderAsync(Guid supplyOrderId, CancellationToken ct = default) =>
        _db.Invoices.FirstOrDefaultAsync(i => i.SupplyOrderId == supplyOrderId, ct);

    public Task<List<Invoice>> ListAsync(Guid? supplierId, CancellationToken ct = default) =>
        _db.Invoices.AsNoTracking().Include(i => i.Order).ThenInclude(o => o!.Customer)
            .Where(i => supplierId == null || i.SupplierId == supplierId)
            .OrderByDescending(i => i.IssuedAtUtc).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
