using Microsoft.AspNetCore.Identity;

namespace SupplierSystem.Web.Models;

/// <summary>
/// Staff account of the Supplier System. Created by the platform admin or seeded; there is no public registration.
/// Managers and staff belong to exactly one supplier and only see that supplier's data.
/// </summary>
public class StaffUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = "";

    /// <summary>Null only for the platform admin, who sees every supplier.</summary>
    public Guid? SupplierId { get; set; }
}

public static class Roles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string SupplierManager = "SupplierManager";
    public const string SupplierStaff = "SupplierStaff";
    public static readonly string[] All = [PlatformAdmin, SupplierManager, SupplierStaff];
}
