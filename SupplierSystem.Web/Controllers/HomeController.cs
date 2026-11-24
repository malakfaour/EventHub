using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Web.Models;

namespace SupplierSystem.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index() => RedirectToAction("Index", "Orders");

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    /// <summary>Friendly page for 404, 403 and other status codes (wired with UseStatusCodePagesWithReExecute).</summary>
    [AllowAnonymous, HttpGet("/status/{code:int}")]
    public IActionResult Status(int code)
    {
        ViewBag.Path = HttpContext.Features.Get<IStatusCodeReExecuteFeature>()?.OriginalPath;
        Response.StatusCode = code;
        return View(code);
    }
}
