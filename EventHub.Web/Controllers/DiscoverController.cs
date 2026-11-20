using EventHub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.Web.Controllers;

/// <summary>The public showcase: anyone can browse published events without an account.</summary>
[AllowAnonymous]
public class DiscoverController : Controller
{
    private readonly ITicketService _tickets;

    public DiscoverController(ITicketService tickets) => _tickets = tickets;

    public async Task<IActionResult> Index(string? search, string? type, CancellationToken ct)
    {
        ViewBag.Search = search;
        ViewBag.Type = type;
        return View(await _tickets.ListPublicAsync(search, type, ct));
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var ev = await _tickets.GetPublicAsync(id, ct);
        return ev is null ? NotFound() : View(ev);
    }
}
