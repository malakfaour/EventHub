using System.Security.Claims;
using EventHub.Web.Models;
using EventHub.Web.Services;
using EventHub.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.Web.Controllers;

[Authorize(Roles = Roles.Organizer)]
public class InvoicesController : Controller
{
    private readonly IInvoiceService _invoices;

    public InvoicesController(IInvoiceService invoices) => _invoices = invoices;

    private Guid OrganizerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _invoices.ListAsync(OrganizerId, ct));

    public async Task<IActionResult> Pay(Guid id, CancellationToken ct)
    {
        var invoice = await _invoices.GetAsync(id, OrganizerId, ct);
        if (invoice is null) return NotFound();
        if (invoice.Status == InvoiceStatus.Paid) return RedirectToAction(nameof(Index));
        ViewBag.Invoice = invoice;
        return View(new PayInvoiceViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(Guid id, PayInvoiceViewModel vm, CancellationToken ct)
    {
        var invoice = await _invoices.GetAsync(id, OrganizerId, ct);
        if (invoice is null) return NotFound();

        if (ModelState.IsValid)
        {
            var error = await _invoices.PayAsync(id, OrganizerId, vm, ct);
            if (error is null)
            {
                TempData["Success"] = "Payment received. The supplier has been notified.";
                return RedirectToAction("Details", "Orders", new { id = invoice.OrderId });
            }
            ModelState.AddModelError("", error);
        }

        ViewBag.Invoice = invoice;
        vm.CardNumber = vm.Cvv = "";   // never send card details back to the browser
        return View(vm);
    }
}
