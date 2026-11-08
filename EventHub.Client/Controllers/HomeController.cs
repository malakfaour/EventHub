using System.Diagnostics;
using EventHub.Client.Models;
using EventHub.Client.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.Client.Controllers;

public class HomeController : Controller
{
    private readonly IEventService _events;

    public HomeController(IEventService events) => _events = events;

    public async Task<IActionResult> Index() => View(await _events.GetPublishedAsync());

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
