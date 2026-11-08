using System.Security.Claims;
using EventHub.Client.Services;
using EventHub.Client.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EventHub.Client.Controllers;

[Authorize]
public class EventsController : Controller
{
    private readonly IEventService _events;

    public EventsController(IEventService events) => _events = events;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new CreateEventViewModel();
        await FillCategoriesAsync(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEventViewModel vm)
    {
        if (vm.StartsAt <= DateTime.UtcNow)
            ModelState.AddModelError(nameof(vm.StartsAt), "The start time must be in the future.");

        if (!ModelState.IsValid)
        {
            await FillCategoriesAsync(vm);
            return View(vm);
        }

        await _events.CreateAndSubmitAsync(vm, CurrentUserId);
        TempData["Success"] = "Event submitted. It stays Pending until a moderator approves it.";
        return RedirectToAction(nameof(My));
    }

    [HttpGet]
    public async Task<IActionResult> My() => View(await _events.GetMyEventsAsync(CurrentUserId));

    private async Task FillCategoriesAsync(CreateEventViewModel vm)
    {
        var categories = await _events.GetCategoriesAsync();
        vm.Categories = categories.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToList();
    }
}
