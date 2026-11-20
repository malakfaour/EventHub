using EventHub.Contracts;
using EventHub.Messaging;
using SupplierSystem.Web.Models;
using SupplierSystem.Web.Repositories;
using SupplierSystem.Web.ViewModels;

namespace SupplierSystem.Web.Services;

/// <summary>Catalog and stock rules. Every catalog change saves and queues a CatalogPublished message together.</summary>
public sealed class CatalogService : ICatalogService
{
    private readonly ICatalogRepository _repo;
    private readonly IOutbox _outbox;
    private readonly ILogger<CatalogService> _logger;

    public CatalogService(ICatalogRepository repo, IOutbox outbox, ILogger<CatalogService> logger)
    {
        _repo = repo;
        _outbox = outbox;
        _logger = logger;
    }

    public Task<List<Supplier>> ListSuppliersAsync(CancellationToken ct = default) => _repo.ListSuppliersAsync(ct);

    public Task<List<Product>> ListProductsAsync(Guid supplierId, CancellationToken ct = default) =>
        _repo.ListProductsAsync(supplierId, ct);

    public async Task<Product?> GetProductAsync(Guid supplierId, Guid productId, CancellationToken ct = default)
    {
        var product = await _repo.GetProductAsync(productId, ct);
        return product is not null && product.SupplierId == supplierId ? product : null; // never another supplier's product
    }

    public async Task CreateProductAsync(Guid supplierId, ProductViewModel vm, CancellationToken ct = default)
    {
        var supplier = await _repo.GetSupplierAsync(supplierId, ct)
                       ?? throw new InvalidOperationException("Supplier not found.");

        // A supplier can only sell in its own category, so a product has no category of its own.
        var product = new Product
        {
            Id = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Name = vm.Name.Trim(),
            Unit = vm.Unit.Trim(),
            Price = vm.Price,
            IsActive = vm.IsActive,
            Inventory = new InventoryItem { QuantityOnHand = vm.QuantityOnHand, MinLevel = vm.MinLevel }
        };

        await _repo.ExecuteInTransactionAsync(async () =>
        {
            await _repo.AddProductAsync(product, ct);
            await _repo.SaveChangesAsync(ct);
            await EnqueueSnapshotAsync(supplier, ct);
            await _repo.SaveChangesAsync(ct);
        }, ct);

        _logger.LogInformation("Product {Product} added for supplier {Supplier}", product.Name, supplier.Name);
    }

    public async Task UpdateProductAsync(Guid supplierId, Guid productId, ProductViewModel vm, CancellationToken ct = default)
    {
        var supplier = await _repo.GetSupplierAsync(supplierId, ct)
                       ?? throw new InvalidOperationException("Supplier not found.");
        var product = await GetProductAsync(supplierId, productId, ct)
                      ?? throw new InvalidOperationException("Product not found.");

        product.Name = vm.Name.Trim();
        product.Unit = vm.Unit.Trim();
        product.Price = vm.Price;
        product.IsActive = vm.IsActive;
        product.Inventory ??= new InventoryItem { ProductId = product.Id };
        product.Inventory.QuantityOnHand = vm.QuantityOnHand;
        product.Inventory.MinLevel = vm.MinLevel;

        await _repo.ExecuteInTransactionAsync(async () =>
        {
            await _repo.SaveChangesAsync(ct);
            await EnqueueSnapshotAsync(supplier, ct);
            await _repo.SaveChangesAsync(ct);
        }, ct);
    }

    public async Task PublishCatalogAsync(Guid supplierId, CancellationToken ct = default)
    {
        var supplier = await _repo.GetSupplierAsync(supplierId, ct)
                       ?? throw new InvalidOperationException("Supplier not found.");
        await EnqueueSnapshotAsync(supplier, ct);
        await _repo.SaveChangesAsync(ct);
    }

    private async Task EnqueueSnapshotAsync(Supplier supplier, CancellationToken ct)
    {
        var products = await _repo.ListProductsAsync(supplier.Id, ct);
        var items = products
            .Select(p => new CatalogItem(p.Id, p.Name, p.Unit, p.Price, p.IsActive && (p.Inventory?.Available ?? 0) > 0))
            .ToList();

        _outbox.Enqueue(new CatalogPublished(supplier.Id, supplier.Name, supplier.Category, items)
        {
            CorrelationId = supplier.Id
        });
    }
}
