using EventHub.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Web.Models;
using SupplierSystem.Web.Repositories;
using SupplierSystem.Web.ViewModels;

namespace SupplierSystem.Web.Services;

/// <summary>Who may use the Supplier System: the platform admin creates suppliers (with a manager), managers create their own staff.</summary>
public interface ITeamService
{
    Task<List<SupplierRow>> ListSuppliersAsync(CancellationToken ct = default);
    Task<IdentityResult> CreateSupplierAsync(NewSupplierViewModel vm, CancellationToken ct = default);
    Task ToggleSupplierAsync(Guid supplierId, CancellationToken ct = default);

    Task<List<TeamMemberRow>> ListStaffAsync(Guid supplierId, CancellationToken ct = default);
    Task<IdentityResult> CreateStaffAsync(Guid supplierId, NewStaffViewModel vm, CancellationToken ct = default);
    Task ToggleStaffAsync(Guid supplierId, Guid userId, Guid currentUserId, CancellationToken ct = default);
}

public sealed class TeamService : ITeamService
{
    private readonly UserManager<StaffUser> _users;
    private readonly ICatalogRepository _catalog;
    private readonly ICatalogService _catalogService;
    private readonly ILogger<TeamService> _logger;

    public TeamService(UserManager<StaffUser> users, ICatalogRepository catalog, ICatalogService catalogService,
        ILogger<TeamService> logger)
    {
        _users = users;
        _catalog = catalog;
        _catalogService = catalogService;
        _logger = logger;
    }

    // ───────────── Suppliers (platform admin) ─────────────

    public async Task<List<SupplierRow>> ListSuppliersAsync(CancellationToken ct = default)
    {
        var rows = new List<SupplierRow>();
        foreach (var s in await _catalog.ListSuppliersAsync(ct))
        {
            var products = (await _catalog.ListProductsAsync(s.Id, ct)).Count;
            var staff = await _users.Users.CountAsync(u => u.SupplierId == s.Id, ct);
            rows.Add(new SupplierRow(s.Id, s.Name, s.Category, s.ContactEmail, s.IsActive, products, staff));
        }
        return rows;
    }

    public async Task<IdentityResult> CreateSupplierAsync(NewSupplierViewModel vm, CancellationToken ct = default)
    {
        if (!SupplyCategories.IsValid(vm.Category))
            return IdentityResult.Failed(new IdentityError { Description = "Choose one of the supply categories." });

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(), Name = vm.Name.Trim(), Category = vm.Category, ContactEmail = vm.ContactEmail.Trim(), IsActive = true
        };
        var manager = new StaffUser
        {
            Id = Guid.NewGuid(), UserName = vm.ManagerEmail.Trim(), Email = vm.ManagerEmail.Trim(), EmailConfirmed = true,
            FullName = vm.ManagerName.Trim(), SupplierId = supplier.Id
        };

        IdentityResult result = IdentityResult.Success;
        try
        {
            // Supplier and manager are saved together, so a rejected password never leaves a supplier without a manager.
            await _catalog.ExecuteInTransactionAsync(async () =>
            {
                await _catalog.AddSupplierAsync(supplier, ct);
                await _catalog.SaveChangesAsync(ct);

                result = await _users.CreateAsync(manager, vm.ManagerPassword);
                if (result.Succeeded) result = await _users.AddToRoleAsync(manager, Roles.SupplierManager);
                if (!result.Succeeded) throw new InvalidOperationException("rollback");
            }, ct);
        }
        catch (InvalidOperationException) when (!result.Succeeded)
        {
            return result;   // the transaction was rolled back
        }

        await _catalogService.PublishCatalogAsync(supplier.Id, ct);   // tell the Event Hub the new supplier exists
        _logger.LogInformation("Supplier {Supplier} created with manager {Manager}", supplier.Name, manager.Email);
        return result;
    }

    public async Task ToggleSupplierAsync(Guid supplierId, CancellationToken ct = default)
    {
        var supplier = await _catalog.GetSupplierAsync(supplierId, ct) ?? throw new InvalidOperationException("Supplier not found.");
        supplier.IsActive = !supplier.IsActive;
        await _catalog.SaveChangesAsync(ct);
        // An inactive supplier's items are published as unavailable, so organizers cannot order from it.
        await _catalogService.PublishCatalogAsync(supplierId, ct);
    }

    // ───────────── Staff (supplier manager) ─────────────

    public async Task<List<TeamMemberRow>> ListStaffAsync(Guid supplierId, CancellationToken ct = default)
    {
        var users = await _users.Users.Where(u => u.SupplierId == supplierId).OrderBy(u => u.FullName).ToListAsync(ct);
        var rows = new List<TeamMemberRow>();
        foreach (var u in users)
        {
            var roles = await _users.GetRolesAsync(u);
            rows.Add(new TeamMemberRow(u.Id, u.FullName, u.Email ?? "", roles.FirstOrDefault() ?? "-", u.LockoutEnd > DateTimeOffset.UtcNow));
        }
        return rows;
    }

    public async Task<IdentityResult> CreateStaffAsync(Guid supplierId, NewStaffViewModel vm, CancellationToken ct = default)
    {
        var user = new StaffUser
        {
            Id = Guid.NewGuid(), UserName = vm.Email.Trim(), Email = vm.Email.Trim(), EmailConfirmed = true,
            FullName = vm.FullName.Trim(), SupplierId = supplierId   // always the manager's own supplier, never from the form
        };
        var result = await _users.CreateAsync(user, vm.Password);
        if (result.Succeeded) result = await _users.AddToRoleAsync(user, Roles.SupplierStaff);
        if (result.Succeeded) _logger.LogInformation("Staff {Email} added to supplier {Supplier}", user.Email, supplierId);
        return result;
    }

    public async Task ToggleStaffAsync(Guid supplierId, Guid userId, Guid currentUserId, CancellationToken ct = default)
    {
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null || user.SupplierId != supplierId) throw new InvalidOperationException("Team member not found.");
        if (user.Id == currentUserId) throw new InvalidOperationException("You cannot disable your own account.");
        if (await _users.IsInRoleAsync(user, Roles.SupplierManager))
            throw new InvalidOperationException("Manager accounts cannot be disabled here.");

        var locked = user.LockoutEnd > DateTimeOffset.UtcNow;
        await _users.SetLockoutEnabledAsync(user, true);
        await _users.SetLockoutEndDateAsync(user, locked ? null : DateTimeOffset.UtcNow.AddYears(100));
        await _users.UpdateSecurityStampAsync(user);
    }
}
