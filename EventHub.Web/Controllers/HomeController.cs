using System.Diagnostics;
using EventHub.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.Web.Controllers;

public class HomeController : Controller
{
    /// <summary>Sends each kind of user to their own start page.</summary>
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated != true) return RedirectToAction("Index", "Discover");
        if (User.IsInRole(Roles.Admin)) return RedirectToAction("Index", "Admin");
        if (User.IsInRole(Roles.Organizer)) return RedirectToAction("Index", "Events");
        return RedirectToAction("Index", "Discover");
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
