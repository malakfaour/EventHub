using System.Security.Claims;
using EventHub.Web.Models;
using EventHub.Web.Services;
using EventHub.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.Web.Controllers;

[Authorize(Roles = Roles.Organizer)]
public class OrdersController : Controller
{
    private readonly IOrderService _orders;
    private readonly IEventService _events;
    private readonly ICatalogService _catalog;
    private readonly UserManager<AppUser> _users;
    private readonly IInvoiceService _invoices;

    public OrdersController(IOrderService orders, IEventService events, ICatalogService catalog, UserManager<AppUser> users, IInvoiceService invoices)
    {
        _invoices = invoices;
        _orders = orders;
        _events = events;
        _catalog = catalog;
        _users = users;
    }

    private Guid OrganizerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _orders.ListAsync(OrganizerId, ct));

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var order = await _orders.GetAsync(id, OrganizerId, ct);
        if (order is null) return NotFound();
        ViewBag.Invoice = await _invoices.GetByOrderAsync(order.Id, OrganizerId, ct);
        return View(order);
    }

    /// <summary>Step 1: the suppliers that sell this category.</summary>
    public async Task<IActionResult> Suppliers(Guid eventId, string category, CancellationToken ct)
    {
        var ev = await _events.GetAsync(eventId, OrganizerId, ct);
        if (ev is null) return NotFound();
        ViewBag.Event = ev;
        ViewBag.Category = category;
        return View(await _catalog.ListSuppliersAsync(category, ct));
    }

    /// <summary>Step 2: choose quantities from one supplier's catalog.</summary>
    public async Task<IActionResult> Create(Guid eventId, Guid supplierId, CancellationToken ct)
    {
        var ev = await _events.GetAsync(eventId, OrganizerId, ct);
        var supplier = await _catalog.GetSupplierAsync(supplierId, ct);
        if (ev is null || supplier is null) return NotFound();

        ViewBag.Event = ev;
        ViewBag.Supplier = supplier;
        return View(new PlaceOrderViewModel
        {
            EventId = ev.Id,
            SupplierId = supplier.Id,
            DeliveryAtUtc = ev.StartsAtUtc.AddHours(-3),
            DeliveryAddress = ev.Venue,
            Items = supplier.Products.OrderBy(p => p.Name).Select(p => new OrderItemInput { ProductId = p.Id }).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PlaceOrderViewModel vm, CancellationToken ct)
    {
        var organizer = await _users.GetUserAsync(User);
        if (organizer is null) return Challenge();

        if (ModelState.IsValid)
        {
            try
            {
                var id = await _orders.PlaceAsync(organizer, vm, ct);
                TempData["Success"] = "Order sent to the supplier.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
        }

        ViewBag.Event = await _events.GetAsync(vm.EventId, OrganizerId, ct);
        ViewBag.Supplier = await _catalog.GetSupplierAsync(vm.SupplierId, ct);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        try
        {
            await _orders.CancelAsync(id, OrganizerId, "Cancelled by the organizer", ct);
            TempData["Success"] = "Order cancelled.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}
