using System.Security.Claims;
using EventHub.Web.Models;
using EventHub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.Web.Controllers;

/// <summary>Guest list and ticket check-in for one of the organizer's events.</summary>
[Authorize(Roles = Roles.Organizer)]
public class DoorController : Controller
{
    private readonly IDoorService _door;

    public DoorController(IDoorService door) => _door = door;

    private Guid OrganizerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Guests(Guid id, CancellationToken ct)
    {
        var ev = await _door.GetEventAsync(id, OrganizerId, ct);
        return ev is null ? NotFound() : View(ev);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddGuest(Guid id, string name, string? email, int partySize, CancellationToken ct)
    {
        try { await _door.AddGuestAsync(id, OrganizerId, name, email, partySize <= 0 ? 1 : partySize, ct); TempData["Success"] = "Guest added."; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Guests), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveGuest(Guid id, Guid guestId, CancellationToken ct)
    {
        try { await _door.RemoveGuestAsync(id, OrganizerId, guestId, ct); TempData["Success"] = "Guest removed."; }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Guests), new { id });
    }

    public async Task<IActionResult> CheckIn(Guid id, CancellationToken ct)
    {
        var ev = await _door.GetEventAsync(id, OrganizerId, ct);
        if (ev is null) return NotFound();
        ViewBag.Tickets = await _door.ListTicketsAsync(id, OrganizerId, ct);
        return View(ev);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn(Guid id, string code, CancellationToken ct)
    {
        try
        {
            var result = await _door.CheckInAsync(id, OrganizerId, code, ct);
            TempData[result.Ok ? "Success" : "Error"] = result.Message;
        }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(CheckIn), new { id });
    }
}
