using System.Security.Claims;
using EventHub.Web.Models;
using EventHub.Web.Services;
using EventHub.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.Web.Controllers;

[Authorize(Roles = Roles.Organizer)]
public class EventsController : Controller
{
    private readonly IEventService _events;
    private readonly IOrderService _orders;
    private readonly EventHub.Web.Services.Ai.IEventAdvisor _advisor;

    public EventsController(IEventService events, IOrderService orders, EventHub.Web.Services.Ai.IEventAdvisor advisor)
    {
        _advisor = advisor;
        _events = events;
        _orders = orders;
    }

    private Guid OrganizerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>My events. <paramref name="show"/> filters the table: upcoming, drafts or past (empty = all). The stat cards always count every event.</summary>
    public async Task<IActionResult> Index(string? show, CancellationToken ct)
    {
        ViewBag.Show = show is "upcoming" or "drafts" or "past" ? show : null;
        return View(await _events.ListAsync(OrganizerId, ct));
    }

    public IActionResult Create() => View("Form", new EventFormViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventFormViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Form", vm);
        var id = await _events.CreateAsync(OrganizerId, vm, ct);
        TempData["Success"] = "Event created. Now list what it needs.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var ev = await _events.GetAsync(id, OrganizerId, ct);
        if (ev is null) return NotFound();
        ViewBag.Orders = await _orders.ListForEventAsync(id, OrganizerId, ct);
        return View(ev);
    }

    /// <summary>Asks the advisor (AI, or rules as a fallback) what this event needs. Read-only: nothing is added until the organizer chooses.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Suggest(Guid id, CancellationToken ct)
    {
        var ev = await _events.GetAsync(id, OrganizerId, ct);
        if (ev is null) return NotFound();
        ViewBag.Advice = await _advisor.SuggestAsync(ev, ct);
        return View(ev);
    }

    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var ev = await _events.GetAsync(id, OrganizerId, ct);
        if (ev is null) return NotFound();
        ViewBag.EventId = id;
        return View("Form", new EventFormViewModel
        {
            Name = ev.Name, Description = ev.Description, Venue = ev.Venue,
            StartsAtUtc = ev.StartsAtUtc, ExpectedGuests = ev.ExpectedGuests,
            EventType = ev.EventType, ImageUrl = ev.ImageUrl
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, EventFormViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid) { ViewBag.EventId = id; return View("Form", vm); }
        return await RunAsync(id, () => _events.UpdateAsync(id, OrganizerId, vm, ct), "Event saved.");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> AddRequirement(Guid id, RequirementViewModel vm, CancellationToken ct) =>
        RunAsync(id, () => _events.AddRequirementAsync(id, OrganizerId, vm, ct), "Requirement added.");

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> RemoveRequirement(Guid id, Guid requirementId, CancellationToken ct) =>
        RunAsync(id, () => _events.RemoveRequirementAsync(id, OrganizerId, requirementId, ct), "Requirement removed.");

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> AddTicketType(Guid id, TicketTypeViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Check the ticket type: a name, a price of 0 or more, and a quantity of at least 1.";
            return Task.FromResult<IActionResult>(RedirectToAction(nameof(Details), new { id }));
        }
        return RunAsync(id, () => _events.AddTicketTypeAsync(id, OrganizerId, vm, ct), "Ticket type added.");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> RemoveTicketType(Guid id, Guid ticketTypeId, CancellationToken ct) =>
        RunAsync(id, () => _events.RemoveTicketTypeAsync(id, OrganizerId, ticketTypeId, ct), "Ticket type removed.");

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Publish(Guid id, CancellationToken ct) =>
        RunAsync(id, () => _events.PublishAsync(id, OrganizerId, ct), "Published. Clients can now buy tickets.");

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Unpublish(Guid id, CancellationToken ct) =>
        RunAsync(id, () => _events.UnpublishAsync(id, OrganizerId, ct), "Event hidden from clients.");

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Plan(Guid id, CancellationToken ct) =>
        RunAsync(id, () => _events.PlanAsync(id, OrganizerId, ct), "Event is now planned.");

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Complete(Guid id, CancellationToken ct) =>
        RunAsync(id, () => _events.CompleteAsync(id, OrganizerId, ct), "Event completed.");

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Cancel(Guid id, CancellationToken ct) =>
        RunAsync(id, () => _events.CancelAsync(id, OrganizerId, ct), "Event cancelled. Open orders were cancelled too.");

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
