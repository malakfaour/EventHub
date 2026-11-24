using EventHub.Contracts;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Web.Data;
using SupplierSystem.Web.Models;

namespace SupplierSystem.Web.Repositories;

/// <summary>EF Core data access. Repositories hold queries; business rules live in the services.</summary>
public sealed class CatalogRepository : ICatalogRepository
{
    private readonly AppDbContext _db;

    public CatalogRepository(AppDbContext db) => _db = db;

    public Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken ct = default) =>
        _db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<List<Supplier>> ListSuppliersAsync(CancellationToken ct = default) =>
        _db.Suppliers.AsNoTracking().OrderBy(s => s.Category).ThenBy(s => s.Name).ToListAsync(ct);

    public Task<List<Product>> ListProductsAsync(Guid supplierId, CancellationToken ct = default) =>
        _db.Products.Include(p => p.Inventory).Where(p => p.SupplierId == supplierId)
            .OrderBy(p => p.Name).ToListAsync(ct);

    public Task<Product?> GetProductAsync(Guid id, CancellationToken ct = default) =>
        _db.Products.Include(p => p.Inventory).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<List<Product>> GetProductsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        _db.Products.Include(p => p.Inventory).Where(p => ids.Contains(p.Id)).ToListAsync(ct);

    public async Task AddProductAsync(Product product, CancellationToken ct = default) =>
        await _db.Products.AddAsync(product, ct);

    public async Task AddSupplierAsync(Supplier supplier, CancellationToken ct = default) =>
        await _db.Suppliers.AddAsync(supplier, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await work();
        await tx.CommitAsync(ct);
    }
}

public sealed class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;

    public OrderRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(SupplyOrder order, CancellationToken ct = default) => await _db.Orders.AddAsync(order, ct);

    public Task<SupplyOrder?> GetAsync(Guid id, CancellationToken ct = default) =>
        _db.Orders.Include(o => o.Lines).Include(o => o.Customer).Include(o => o.Supplier)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<SupplyOrder?> GetByExternalIdAsync(Guid externalOrderId, CancellationToken ct = default) =>
        _db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.ExternalOrderId == externalOrderId, ct);

    public Task<bool> ExistsByExternalIdAsync(Guid externalOrderId, CancellationToken ct = default) =>
        _db.Orders.AnyAsync(o => o.ExternalOrderId == externalOrderId, ct);

    public Task<List<SupplyOrder>> ListAsync(Guid? supplierId, SupplyOrderStatus? status, CancellationToken ct = default)
    {
        var query = _db.Orders.AsNoTracking().Include(o => o.Customer).Include(o => o.Supplier).AsQueryable();
        if (supplierId is Guid sid) query = query.Where(o => o.SupplierId == sid);
        if (status is SupplyOrderStatus s) query = query.Where(o => o.Status == s);
        return query.OrderByDescending(o => o.ReceivedAtUtc).ToListAsync(ct);
    }

    public Task<Customer?> GetCustomerByExternalIdAsync(Guid externalOrganizerId, CancellationToken ct = default) =>
        _db.Customers.FirstOrDefaultAsync(c => c.ExternalOrganizerId == externalOrganizerId, ct);

    public async Task AddCustomerAsync(Customer customer, CancellationToken ct = default) =>
        await _db.Customers.AddAsync(customer, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
