using System.Security.Claims;
using EventHub.Admin.Models;
using EventHub.Admin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.Admin.Controllers;

[Authorize(Policy = "CanModerate")]
public class ModerationController : Controller
{
    private readonly IModerationService _moderation;

    public ModerationController(IModerationService moderation) => _moderation = moderation;

    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> Index(ModerationStatus status = ModerationStatus.Pending)
    {
        ViewData["Status"] = status;
        ViewData["Categories"] = await _moderation.GetCategoryNamesAsync();
        return View(await _moderation.ListAsync(status));
    }

    [HttpGet]
    public async Task<IActionResult> Review(Guid id)
    {
        var item = await _moderation.GetAsync(id);
        if (item is null) return NotFound();
        ViewData["Categories"] = await _moderation.GetCategoryNamesAsync();
        return View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(Guid id, bool featured)
    {
        try
        {
            await _moderation.ApproveAsync(id, CurrentUserId, featured);
            TempData["Success"] = "Event approved. The Client App will publish it shortly.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(Guid id, string reasonCode, string reasonText)
    {
        if (string.IsNullOrWhiteSpace(reasonText))
        {
            TempData["Error"] = "A reason is required to reject an event.";
            return RedirectToAction(nameof(Review), new { id });
        }

        try
        {
            await _moderation.RejectAsync(id, CurrentUserId, reasonCode, reasonText.Trim());
            TempData["Success"] = "Event rejected. The organizer will see your reason.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }
}
