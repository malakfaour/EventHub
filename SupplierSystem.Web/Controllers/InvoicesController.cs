using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Web.Services;

namespace SupplierSystem.Web.Controllers;

[Authorize]
public class InvoicesController : StaffControllerBase
{
    private readonly IInvoiceService _invoices;

    public InvoicesController(IInvoiceService invoices) => _invoices = invoices;

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _invoices.ListAsync(SupplierScope, ct));

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "CanProcessOrders")]
    public async Task<IActionResult> Issue(Guid orderId, CancellationToken ct)
    {
        try
        {
            await _invoices.IssueAsync(orderId, SupplierScope, ct);
            TempData["Success"] = "Invoice issued and sent to the organizer.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction("Details", "Orders", new { id = orderId });
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Policy = "CanProcessOrders")]
    public async Task<IActionResult> MarkPaid(Guid orderId, CancellationToken ct)
    {
        try
        {
            await _invoices.MarkPaidManuallyAsync(orderId, SupplierScope, ct);
            TempData["Success"] = "Invoice marked as paid.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction("Details", "Orders", new { id = orderId });
    }
}
