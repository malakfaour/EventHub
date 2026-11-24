using System.Text;
using EventHub.Web.Services;
using EventHub.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventHub.Web.Controllers;

/// <summary>The public showcase: anyone can browse published events without an account.</summary>
[AllowAnonymous]
public class DiscoverController : Controller
{
    private readonly ITicketService _tickets;

    public DiscoverController(ITicketService tickets) => _tickets = tickets;

    public async Task<IActionResult> Index([FromQuery] DiscoverQuery query, CancellationToken ct)
    {
        if (!ModelState.IsValid) query = new DiscoverQuery();
        var vm = new DiscoverViewModel
        {
            Query = query,
            Events = await _tickets.ListPublicAsync(query, ct),
            Stats = await _tickets.GetPublicStatsAsync(ct)
        };
        return View(vm);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var ev = await _tickets.GetPublicAsync(id, ct);
        return ev is null ? NotFound() : View(ev);
    }

    /// <summary>"Add to calendar": an .ics file any calendar app can import. Events are stored in UTC.</summary>
    public async Task<IActionResult> Calendar(Guid id, CancellationToken ct)
    {
        var ev = await _tickets.GetPublicAsync(id, ct);
        if (ev is null) return NotFound();

        static string Esc(string? s) => (s ?? "").Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r", "").Replace("\n", "\\n");
        const string fmt = "yyyyMMdd'T'HHmmss'Z'";
        var url = Url.Action(nameof(Details), "Discover", new { id }, Request.Scheme);

        var ics = new StringBuilder()
            .Append("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//Event Hub//EN\r\nCALSCALE:GREGORIAN\r\nBEGIN:VEVENT\r\n")
            .Append($"UID:{ev.Id}@eventhub\r\n")
            .Append($"DTSTAMP:{DateTime.UtcNow.ToString(fmt)}\r\n")
            .Append($"DTSTART:{ev.StartsAtUtc.ToString(fmt)}\r\n")
            .Append($"DTEND:{ev.StartsAtUtc.AddHours(3).ToString(fmt)}\r\n")
            .Append($"SUMMARY:{Esc(ev.Name)}\r\n")
            .Append($"LOCATION:{Esc(ev.Venue)}\r\n")
            .Append($"DESCRIPTION:{Esc(ev.Description)}\\n\\n{Esc(url)}\r\n")
            .Append($"URL:{url}\r\n")
            .Append("END:VEVENT\r\nEND:VCALENDAR\r\n")
            .ToString();

        var safeName = new string(ev.Name.Where(char.IsLetterOrDigit).ToArray());
        return File(Encoding.UTF8.GetBytes(ics), "text/calendar", $"{(safeName.Length > 0 ? safeName : "event")}.ics");
    }
}
