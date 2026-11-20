using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SupplierSystem.Web.Controllers;
using SupplierSystem.Web.Models;

namespace SupplierSystem.Web.Services;

/// <summary>Adds the staff member's supplier id to the login cookie, so every request knows which supplier it belongs to.</summary>
public sealed class StaffClaimsFactory : UserClaimsPrincipalFactory<StaffUser, IdentityRole<Guid>>
{
    public StaffClaimsFactory(UserManager<StaffUser> users, RoleManager<IdentityRole<Guid>> roles, IOptions<IdentityOptions> options)
        : base(users, roles, options) { }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(StaffUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (user.SupplierId is Guid supplierId)
            identity.AddClaim(new Claim(StaffControllerBase.SupplierClaim, supplierId.ToString()));
        return identity;
    }
}
