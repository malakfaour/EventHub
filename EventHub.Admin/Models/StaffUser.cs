using Microsoft.AspNetCore.Identity;

namespace EventHub.Admin.Models;

/// <summary>Staff account (Admin, Moderator or Analyst). Created by an Admin or seeded; there is no public registration.</summary>
public class StaffUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = "";
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Moderator = "Moderator";
    public const string Analyst = "Analyst";
    public static readonly string[] All = [Admin, Moderator, Analyst];
}
