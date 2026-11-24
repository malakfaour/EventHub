using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupplierSystem.Web.Controllers;
using SupplierSystem.Web.Data;
using SupplierSystem.Web.Models;

namespace SupplierSystem.Web.Services;

/// <summary>
/// Adds the staff member's supplier to the login cookie, so every request knows which supplier it belongs to.
/// The name and category are only for display (the colored supplier badge); access checks use the id.
/// </summary>
public sealed class StaffClaimsFactory : UserClaimsPrincipalFactory<StaffUser, IdentityRole<Guid>>
{
    public const string SupplierNameClaim = "supplier_name";
    public const string SupplierCategoryClaim = "supplier_category";

    private readonly AppDbContext _db;

    public StaffClaimsFactory(UserManager<StaffUser> users, RoleManager<IdentityRole<Guid>> roles, IOptions<IdentityOptions> options, AppDbContext db)
        : base(users, roles, options) => _db = db;

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(StaffUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.SupplierId is Guid supplierId)
        {
            identity.AddClaim(new Claim(StaffControllerBase.SupplierClaim, supplierId.ToString()));
            var supplier = await _db.Suppliers.AsNoTracking()
                .Where(s => s.Id == supplierId).Select(s => new { s.Name, s.Category }).FirstOrDefaultAsync();
            if (supplier is not null)
            {
                identity.AddClaim(new Claim(SupplierNameClaim, supplier.Name));
                identity.AddClaim(new Claim(SupplierCategoryClaim, supplier.Category));
            }
        }
        return identity;
    }
}
