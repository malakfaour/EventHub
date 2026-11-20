using EventHub.Contracts;
using SupplierSystem.Web.Models;
using SupplierSystem.Web.ViewModels;

namespace SupplierSystem.Web.Services;

public interface ICatalogService
{
    Task<List<Supplier>> ListSuppliersAsync(CancellationToken ct = default);
    Task<List<Product>> ListProductsAsync(Guid supplierId, CancellationToken ct = default);
    Task<Product?> GetProductAsync(Guid supplierId, Guid productId, CancellationToken ct = default);
    Task CreateProductAsync(Guid supplierId, ProductViewModel vm, CancellationToken ct = default);
    Task UpdateProductAsync(Guid supplierId, Guid productId, ProductViewModel vm, CancellationToken ct = default);

    /// <summary>Queues a CatalogPublished snapshot of one supplier's catalog.</summary>
    Task PublishCatalogAsync(Guid supplierId, CancellationToken ct = default);
}

public interface IOrderService
{
    // Called by the consumers (messages from the Event Hub)
    Task ReceiveAsync(SupplyOrderPlaced message, CancellationToken ct = default);
    Task ApplyCancelledAsync(OrderCancelled message, CancellationToken ct = default);

    // Called by staff. supplierScope null = platform admin (all suppliers).
    Task<List<SupplyOrder>> ListAsync(Guid? supplierScope, SupplyOrderStatus? status, CancellationToken ct = default);
    Task<SupplyOrder?> GetAsync(Guid id, Guid? supplierScope, CancellationToken ct = default);
    Task AcceptAsync(Guid id, Guid? supplierScope, CancellationToken ct = default);
    Task RejectAsync(Guid id, Guid? supplierScope, string reasonCode, string reasonText, CancellationToken ct = default);
    Task AdvanceAsync(Guid id, Guid? supplierScope, SupplyOrderStatus next, CancellationToken ct = default);
}
