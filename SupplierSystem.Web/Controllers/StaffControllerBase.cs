using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SupplierSystem.Web.Models;

namespace SupplierSystem.Web.Controllers;

/// <summary>
/// Works out which supplier the signed-in staff member belongs to. Managers and staff are locked to their own supplier;
/// the platform admin (no supplier) sees all of them. The supplier id comes from a claim, never from the URL or a form.
/// </summary>
public abstract class StaffControllerBase : Controller
{
    public const string SupplierClaim = "supplier_id";

    protected Guid? SupplierScope =>
        Guid.TryParse(User.FindFirstValue(SupplierClaim), out var id) ? id : null;

    protected bool IsPlatformAdmin => User.IsInRole(Roles.PlatformAdmin);

    /// <summary>Supplier id for actions that must belong to one supplier (Guid.Empty matches nothing).</summary>
    protected Guid RequiredSupplier => SupplierScope ?? Guid.Empty;
}
