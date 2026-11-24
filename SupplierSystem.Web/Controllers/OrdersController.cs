using EventHub.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Web.Services;
using SupplierSystem.Web.ViewModels;

namespace SupplierSystem.Web.Controllers;

[Authorize]
public class OrdersController : StaffControllerBase
{
    private readonly IOrderService _orders;
    private readonly IInvoiceService _invoices;
    private readonly ICatalogService _catalog;

    public OrdersController(IOrderService orders, IInvoiceService invoices, ICatalogService catalog)
    {
        _orders = orders;
        _invoices = invoices;
        _catalog = catalog;
    }

    public async Task<IActionResult> Index(SupplyOrderStatus? status, CancellationToken ct)
    {
        ViewBag.Status = status;
        return View(await _orders.ListAsync(SupplierScope, status, ct));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var order = await _orders.GetAsync(id, SupplierScope, ct);
        if (order is null) return NotFound();
        ViewBag.Invoice = await _invoices.GetByOrderAsync(order.Id, SupplierScope, ct);
        return View(order);
    }

    [Authorize(Policy = "CanProcessOrders")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var vm = new ManualOrderViewModel();
        await FillItemsAsync(vm, ct);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "CanProcessOrders")]
    public async Task<IActionResult> Create(ManualOrderViewModel vm, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var id = await _orders.CreateManualAsync(RequiredSupplier, vm, ct);
                TempData["Success"] = "Order recorded.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
        }
        var quantities = vm.Items.ToDictionary(i => i.ProductId, i => i.Quantity);
        await FillItemsAsync(vm, ct);
        foreach (var item in vm.Items) if (quantities.TryGetValue(item.ProductId, out var q)) item.Quantity = q;
        return View(vm);
    }

    private async Task FillItemsAsync(ManualOrderViewModel vm, CancellationToken ct)
    {
        var products = await _catalog.ListProductsAsync(RequiredSupplier, ct);
        ViewBag.Products = products.Where(p => p.IsActive).OrderBy(p => p.Name).ToList();
        vm.Items = ((List<SupplierSystem.Web.Models.Product>)ViewBag.Products)
            .Select(p => new ManualOrderItem { ProductId = p.Id }).ToList();
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "CanProcessOrders")]
    public Task<IActionResult> Accept(Guid id, CancellationToken ct) =>
        RunAsync(id, () => _orders.AcceptAsync(id, SupplierScope, ct), "Order accepted. Stock is reserved.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "CanProcessOrders")]
    public Task<IActionResult> Reject(Guid id, string reasonCode, string? reasonText, CancellationToken ct) =>
        RunAsync(id, () => _orders.RejectAsync(id, SupplierScope, reasonCode, reasonText ?? reasonCode, ct), "Order rejected.");

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "CanProcessOrders")]
    public Task<IActionResult> Advance(Guid id, SupplyOrderStatus next, CancellationToken ct) =>
        RunAsync(id, () => _orders.AdvanceAsync(id, SupplierScope, next, ct), $"Order is now {next}.");

    private async Task<IActionResult> RunAsync(Guid id, Func<Task> action, string success)
    {
        try
        {
            await action();
            TempData["Success"] = success;
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}
