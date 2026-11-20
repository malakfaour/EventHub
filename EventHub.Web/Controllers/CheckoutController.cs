using System.Security.Claims;
using EventHub.Web.Models;
using EventHub.Web.Services;
using EventHub.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.Web.Controllers;

[Authorize(Roles = Roles.Client)]
public class CheckoutController : Controller
{
    private readonly ITicketService _tickets;
    private readonly UserManager<AppUser> _users;

    public CheckoutController(ITicketService tickets, UserManager<AppUser> users)
    {
        _tickets = tickets;
        _users = users;
    }

    public async Task<IActionResult> Index(Guid eventId, Guid ticketTypeId, int quantity = 1, CancellationToken ct = default)
    {
        var ev = await _tickets.GetPublicAsync(eventId, ct);
        if (ev is null || ev.TicketTypes.All(t => t.Id != ticketTypeId)) return NotFound();

        var user = await _users.GetUserAsync(User);
        ViewBag.Event = ev;
        return View(new CheckoutViewModel
        {
            EventId = eventId, TicketTypeId = ticketTypeId, Quantity = Math.Clamp(quantity, 1, 10),
            CardHolder = user?.DisplayName ?? ""
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(CheckoutViewModel vm, CancellationToken ct)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Challenge();

        if (ModelState.IsValid)
        {
            try
            {
                var id = await _tickets.PurchaseAsync(user.Id, user.DisplayName, vm, ct);
                TempData["Success"] = "Payment received. Your tickets are ready!";
                return RedirectToAction("Details", "MyTickets", new { id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("", ex.Message);
            }
        }

        // Never send the card number back to the page.
        vm.CardNumber = "";
        vm.Cvv = "";
        ViewBag.Event = await _tickets.GetPublicAsync(vm.EventId, ct);
        if (ViewBag.Event is null) return NotFound();
        return View("Index", vm);
    }
}
