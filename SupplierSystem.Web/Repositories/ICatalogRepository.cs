using EventHub.Contracts;
using SupplierSystem.Web.Models;

namespace SupplierSystem.Web.Repositories;

public interface ICatalogRepository
{
    Task<Supplier?> GetSupplierAsync(Guid id, CancellationToken ct = default);
    Task<List<Supplier>> ListSuppliersAsync(CancellationToken ct = default);
    Task<List<Product>> ListProductsAsync(Guid supplierId, CancellationToken ct = default);
    Task<Product?> GetProductAsync(Guid id, CancellationToken ct = default);
    Task<List<Product>> GetProductsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task AddProductAsync(Product product, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Runs the work and commits everything it saved (data and outbox rows) as one transaction.</summary>
    Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct = default);
}

public interface IOrderRepository
{
    Task AddAsync(SupplyOrder order, CancellationToken ct = default);
    Task<SupplyOrder?> GetAsync(Guid id, CancellationToken ct = default);
    Task<SupplyOrder?> GetByExternalIdAsync(Guid externalOrderId, CancellationToken ct = default);
    Task<bool> ExistsByExternalIdAsync(Guid externalOrderId, CancellationToken ct = default);
    Task<List<SupplyOrder>> ListAsync(Guid? supplierId, SupplyOrderStatus? status, CancellationToken ct = default);
    Task<Customer?> GetCustomerByExternalIdAsync(Guid externalOrganizerId, CancellationToken ct = default);
    Task AddCustomerAsync(Customer customer, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
