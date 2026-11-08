using Microsoft.AspNetCore.Identity;

namespace EventHub.Client.Models;

/// <summary>A portal member. Extends Identity's user with profile and moderation fields.</summary>
public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? City { get; set; }
    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }
    public bool IsPremium { get; set; }
}
