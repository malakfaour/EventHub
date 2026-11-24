using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Web.Models;
using SupplierSystem.Web.Services;
using SupplierSystem.Web.ViewModels;

namespace SupplierSystem.Web.Controllers;

/// <summary>A supplier manager adds and disables staff for their own supplier only.</summary>
[Authorize(Roles = Roles.SupplierManager)]
public class TeamController : StaffControllerBase
{
    private readonly ITeamService _team;

    public TeamController(ITeamService team) => _team = team;

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _team.ListStaffAsync(RequiredSupplier, ct));

    public IActionResult Create() => View(new NewStaffViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NewStaffViewModel vm, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await _team.CreateStaffAsync(RequiredSupplier, vm, ct);
            if (result.Succeeded)
            {
                TempData["Success"] = $"{vm.FullName} can now sign in with {vm.Email}.";
                return RedirectToAction(nameof(Index));
            }
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
        }
        vm.Password = "";
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct)
    {
        try
        {
            await _team.ToggleStaffAsync(RequiredSupplier, id, Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);
            TempData["Success"] = "Account updated.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
