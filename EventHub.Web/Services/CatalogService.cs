using EventHub.Contracts;
using EventHub.Web.Models;
using EventHub.Web.Repositories;

namespace EventHub.Web.Services;

/// <summary>Keeps this app's local copy of the suppliers' catalogs in step with what the Supplier System publishes.</summary>
public sealed class CatalogService : ICatalogService
{
    private readonly ICatalogRepository _repo;

    public CatalogService(ICatalogRepository repo) => _repo = repo;

    public Task<List<CatalogSupplier>> ListSuppliersAsync(string? category, CancellationToken ct = default) =>
        _repo.ListSuppliersAsync(category, ct);

    public Task<CatalogSupplier?> GetSupplierAsync(Guid id, CancellationToken ct = default) =>
        _repo.GetSupplierAsync(id, ct);

    public async Task ApplyPublishedAsync(CatalogPublished m, CancellationToken ct = default)
    {
        var supplier = await _repo.GetSupplierAsync(m.SupplierId, ct);
        if (supplier is null)
        {
            supplier = new CatalogSupplier { Id = m.SupplierId };
            await _repo.AddSupplierAsync(supplier, ct);
        }

        supplier.Name = m.SupplierName;
        supplier.Category = m.Category;
        supplier.UpdatedAtUtc = DateTime.UtcNow;

        // The message is a full snapshot: update what exists, add what is new, hide what disappeared.
        var incoming = m.Items.ToDictionary(i => i.ProductId);
        foreach (var product in supplier.Products)
        {
            if (incoming.Remove(product.Id, out var item))
            {
                product.Name = item.Name; product.Unit = item.Unit; product.Price = item.Price; product.IsAvailable = item.IsAvailable;
            }
            else
            {
                product.IsAvailable = false;
            }
        }
        foreach (var item in incoming.Values)
        {
            supplier.Products.Add(new CatalogProduct
            {
                Id = item.ProductId, SupplierId = supplier.Id, Name = item.Name,
                Unit = item.Unit, Price = item.Price, IsAvailable = item.IsAvailable
            });
        }

        await _repo.SaveChangesAsync(ct);
    }

    public async Task ApplyAvailabilityAsync(ItemAvailabilityChanged m, CancellationToken ct = default)
    {
        var product = await _repo.GetProductAsync(m.ProductId, ct);
        if (product is null) return;   // catalog snapshot not here yet; the next CatalogPublished will carry the state
        product.IsAvailable = m.IsAvailable;
        await _repo.SaveChangesAsync(ct);
    }
}
