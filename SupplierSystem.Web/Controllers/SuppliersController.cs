using EventHub.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Web.Models;
using SupplierSystem.Web.Services;
using SupplierSystem.Web.ViewModels;

namespace SupplierSystem.Web.Controllers;

/// <summary>Platform admin only: onboard suppliers (each with a manager account) and switch them on or off.</summary>
[Authorize(Roles = Roles.PlatformAdmin)]
public class SuppliersController : Controller
{
    private readonly ITeamService _team;

    public SuppliersController(ITeamService team) => _team = team;

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _team.ListSuppliersAsync(ct));

    public IActionResult Create() => View(new NewSupplierViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NewSupplierViewModel vm, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await _team.CreateSupplierAsync(vm, ct);
            if (result.Succeeded)
            {
                TempData["Success"] = $"{vm.Name} was created. The manager can sign in with {vm.ManagerEmail}.";
                return RedirectToAction(nameof(Index));
            }
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
        }
        vm.ManagerPassword = "";
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct)
    {
        try
        {
            await _team.ToggleSupplierAsync(id, ct);
            TempData["Success"] = "Supplier updated.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
