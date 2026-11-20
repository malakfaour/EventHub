using EventHub.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Web.Services;

namespace SupplierSystem.Web.Controllers;

[Authorize]
public class OrdersController : StaffControllerBase
{
    private readonly IOrderService _orders;
    private readonly IInvoiceService _invoices;

    public OrdersController(IOrderService orders, IInvoiceService invoices)
    {
        _orders = orders;
        _invoices = invoices;
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
