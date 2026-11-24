using System.Diagnostics;
using System.Security.Claims;
using EventHub.Web.Models;
using EventHub.Web.Services;
using EventHub.Web.ViewModels;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EventHub.Web.Controllers;

/// <summary>Start page routing plus the public pages: About, Contact, Privacy and the friendly error pages.</summary>
public class HomeController : Controller
{
    private readonly ITicketService _tickets;
    private readonly ICatalogService _catalog;
    private readonly IContactService _contact;
    private readonly UserManager<AppUser> _users;

    public HomeController(ITicketService tickets, ICatalogService catalog, IContactService contact, UserManager<AppUser> users)
    {
        _tickets = tickets;
        _catalog = catalog;
        _contact = contact;
        _users = users;
    }

    /// <summary>Sends each kind of user to their own start page.</summary>
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction("Index", "Discover");
        if (User.IsInRole(Roles.Admin)) return RedirectToAction("Index", "Admin");
        if (User.IsInRole(Roles.Organizer)) return RedirectToAction("Index", "Events");
        return RedirectToAction("Index", "Discover");
    }

    [HttpGet("/about")]
    public async Task<IActionResult> About(CancellationToken ct)
    {
        var stats = await _tickets.GetPublicStatsAsync(ct);
        var suppliers = (await _catalog.ListSuppliersAsync(null, ct)).Count;
        var organizers = (await _users.GetUsersInRoleAsync(Roles.Organizer)).Count;
        return View(new AboutStats(stats.UpcomingEvents, stats.TicketsSold, suppliers, organizers));
    }

    [HttpGet("/contact")]
    public async Task<IActionResult> Contact(string? topic)
    {
        var vm = new ContactFormViewModel();
        if (topic is not null && ContactTopics.All.Contains(topic)) vm.Topic = topic;
        if (User.Identity?.IsAuthenticated == true && await _users.GetUserAsync(User) is { } me)
        {
            vm.Name = me.DisplayName ?? "";
            vm.Email = me.Email ?? "";
        }
        return View(vm);
    }

    [HttpPost("/contact"), ValidateAntiForgeryToken, EnableRateLimiting("contact")]
    public async Task<IActionResult> Contact(ContactFormViewModel vm, CancellationToken ct)
    {
        // Honeypot: a real visitor never sees this field. Pretend it worked so the bot learns nothing.
        if (!string.IsNullOrEmpty(vm.Website))
        {
            TempData["Sent"] = true;
            return RedirectToAction(nameof(Contact));
        }
        if (!ContactTopics.All.Contains(vm.Topic)) ModelState.AddModelError(nameof(vm.Topic), "Choose a topic from the list.");
        if (!ModelState.IsValid) return View(vm);

        Guid? userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        await _contact.SubmitAsync(vm, userId, ct);
        TempData["Sent"] = true;
        return RedirectToAction(nameof(Contact));
    }

    [HttpGet("/privacy")]
    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    /// <summary>Friendly page for 404, 403, 429 and other status codes (wired with UseStatusCodePagesWithReExecute).</summary>
    [HttpGet("/status/{code:int}")]
    public IActionResult Status(int code)
    {
        var feature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();
        ViewBag.Path = feature?.OriginalPath;
        Response.StatusCode = code;
        return View(code);
    }
}
