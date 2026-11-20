using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Web.Services;
using SupplierSystem.Web.ViewModels;

namespace SupplierSystem.Web.Controllers;

[Authorize]
public class ProductsController : StaffControllerBase
{
    private readonly ICatalogService _catalog;

    public ProductsController(ICatalogService catalog) => _catalog = catalog;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        if (SupplierScope is not Guid supplierId)
            return View("Suppliers", await _catalog.ListSuppliersAsync(ct)); // platform admin: overview only
        return View(await _catalog.ListProductsAsync(supplierId, ct));
    }

    [Authorize(Policy = "CanManageCatalog")]
    public IActionResult Create() => View("Form", new ProductViewModel());

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "CanManageCatalog")]
    public async Task<IActionResult> Create(ProductViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Form", vm);
        await _catalog.CreateProductAsync(RequiredSupplier, vm, ct);
        TempData["Success"] = "Product added and the catalog was published to the Event Hub.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = "CanManageCatalog")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var p = await _catalog.GetProductAsync(RequiredSupplier, id, ct);
        if (p is null) return NotFound();
        return View("Form", new ProductViewModel
        {
            Name = p.Name, Unit = p.Unit, Price = p.Price, IsActive = p.IsActive,
            QuantityOnHand = p.Inventory?.QuantityOnHand ?? 0, MinLevel = p.Inventory?.MinLevel ?? 0
        });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "CanManageCatalog")]
    public async Task<IActionResult> Edit(Guid id, ProductViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Form", vm);
        try
        {
            await _catalog.UpdateProductAsync(RequiredSupplier, id, vm, ct);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
        TempData["Success"] = "Product saved and the catalog was published to the Event Hub.";
        return RedirectToAction(nameof(Index));
    }
}
