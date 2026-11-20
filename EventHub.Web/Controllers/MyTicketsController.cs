using System.Security.Claims;
using EventHub.Web.Models;
using EventHub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.Web.Controllers;

[Authorize(Roles = Roles.Client)]
public class MyTicketsController : Controller
{
    private readonly ITicketService _tickets;

    public MyTicketsController(ITicketService tickets) => _tickets = tickets;

    private Guid ClientId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _tickets.ListMineAsync(ClientId, ct));

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var purchase = await _tickets.GetMineAsync(id, ClientId, ct);
        return purchase is null ? NotFound() : View(purchase);
    }
}
