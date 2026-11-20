using EventHub.Web.Models;
using EventHub.Web.Repositories;
using EventHub.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.Web.Controllers;

/// <summary>Platform administration: oversight of users, events, orders, tickets and messaging, plus reports.</summary>
[Authorize(Roles = Roles.Admin)]
public class AdminController : Controller
{
    private readonly IAdminService _admin;
    private readonly IAdminRepository _repo;
    private readonly UserManager<AppUser> _users;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IAdminService admin, IAdminRepository repo, UserManager<AppUser> users, ILogger<AdminController> logger)
    {
        _admin = admin;
        _repo = repo;
        _users = users;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _admin.GetReportAsync(ct));

    public async Task<IActionResult> Users(CancellationToken ct) => View(await _repo.ListUsersAsync(ct));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUser(Guid id)
    {
        var user = await _users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        if (await _users.IsInRoleAsync(user, Roles.Admin))
        {
            TempData["Error"] = "Administrator accounts cannot be disabled here.";
            return RedirectToAction(nameof(Users));
        }

        var locked = user.LockoutEnd > DateTimeOffset.UtcNow;
        await _users.SetLockoutEnabledAsync(user, true);
        await _users.SetLockoutEndDateAsync(user, locked ? null : DateTimeOffset.UtcNow.AddYears(100));
        await _users.UpdateSecurityStampAsync(user);
        _logger.LogWarning("Admin {Admin} {Action} account {User}", User.Identity?.Name, locked ? "enabled" : "disabled", user.Email);
        TempData["Success"] = locked ? $"{user.Email} can sign in again." : $"{user.Email} is disabled and cannot sign in.";
        return RedirectToAction(nameof(Users));
    }

    public async Task<IActionResult> Events(CancellationToken ct) => View(await _repo.ListEventsAsync(ct));

    public async Task<IActionResult> Orders(CancellationToken ct) => View(await _repo.ListOrdersAsync(ct));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(Guid id, string reason, CancellationToken ct)
    {
        try
        {
            await _admin.CancelOrderAsync(id, reason, ct);
            TempData["Success"] = "Order cancelled. The supplier has been notified.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Orders));
    }

    public async Task<IActionResult> Tickets(CancellationToken ct) => View(await _repo.ListPurchasesAsync(ct));

    public async Task<IActionResult> Messages(string? status, CancellationToken ct)
    {
        ViewBag.Status = status;
        return View(await _repo.GetMessagesAsync(status, ct));
    }

    public async Task<IActionResult> ExportExcel(CancellationToken ct) =>
        File(await _admin.ExportExcelAsync(ct),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"eventhub-report-{DateTime.UtcNow:yyyyMMdd}.xlsx");

    public async Task<IActionResult> ExportPdf(CancellationToken ct) =>
        File(await _admin.ExportPdfAsync(ct), "application/pdf", $"eventhub-report-{DateTime.UtcNow:yyyyMMdd}.pdf");
}
