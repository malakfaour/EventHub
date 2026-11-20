using Microsoft.AspNetCore.Identity;

namespace EventHub.Web.Models;

/// <summary>An event organizer. Everyone who registers in the Event Hub is an organizer and sees only their own events.</summary>
public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
    public string? Organization { get; set; }
}
