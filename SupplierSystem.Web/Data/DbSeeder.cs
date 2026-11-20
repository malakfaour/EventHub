using EventHub.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Web.Models;
using SupplierSystem.Web.Services;

namespace SupplierSystem.Web.Data;

public static class DbSeeder
{
    private static readonly Guid GoldenFork = DemoData.GoldenFork.Id;
    private static readonly Guid RoyalFeast = DemoData.RoyalFeast.Id;
    private static readonly Guid BloomCo = DemoData.BloomCo.Id;
    private static readonly Guid SoundWave = DemoData.SoundWave.Id;

    /// <summary>Applies migrations (waiting for SQL Server), then seeds roles, demo suppliers with stock, and demo staff.</summary>
    public static async Task MigrateAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var db = sp.GetRequiredService<AppDbContext>();

        if (!db.Database.GetMigrations().Any())
            throw new InvalidOperationException(
                "No EF Core migrations found. Run: dotnet ef migrations add Initial --project SupplierSystem.Web --output-dir Data/Migrations");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                break;
            }
            catch (Exception ex) when (attempt < 60)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}): {Message}", attempt, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }

        var roles = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole<Guid>(role));

        if (!sp.GetRequiredService<IHostEnvironment>().IsDevelopment()) return;

        if (!await db.Suppliers.AnyAsync())
        {
            SeedSuppliers(db);
            await db.SaveChangesAsync();

            // Tell the Event Hub what each supplier sells (goes through the outbox like any catalog change).
            var catalog = sp.GetRequiredService<ICatalogService>();
            foreach (var supplier in await db.Suppliers.AsNoTracking().ToListAsync())
                await catalog.PublishCatalogAsync(supplier.Id);
        }

        var users = sp.GetRequiredService<UserManager<StaffUser>>();
        await EnsureUserAsync(users, logger, "admin@supplier.local", "Platform Admin", Roles.PlatformAdmin, null);
        await EnsureUserAsync(users, logger, "manager.golden@supplier.local", "Golden Fork Manager", Roles.SupplierManager, GoldenFork);
        await EnsureUserAsync(users, logger, "staff.golden@supplier.local", "Golden Fork Staff", Roles.SupplierStaff, GoldenFork);
        await EnsureUserAsync(users, logger, "manager.royal@supplier.local", "Royal Feast Manager", Roles.SupplierManager, RoyalFeast);
        await EnsureUserAsync(users, logger, "manager.bloom@supplier.local", "Bloom & Co Manager", Roles.SupplierManager, BloomCo);
        await EnsureUserAsync(users, logger, "manager.soundwave@supplier.local", "SoundWave Manager", Roles.SupplierManager, SoundWave);
    }

    private static void SeedSuppliers(AppDbContext db)
    {
        foreach (var d in DemoData.Suppliers)
        {
            db.Suppliers.Add(new Supplier { Id = d.Id, Name = d.Name, Category = d.Category, ContactEmail = d.Email });
            foreach (var p in d.Products)
            {
                db.Products.Add(new Product
                {
                    Id = p.Id, SupplierId = d.Id, Name = p.Name, Unit = p.Unit, Price = p.Price,
                    Inventory = new InventoryItem { QuantityOnHand = p.Stock, MinLevel = p.MinLevel }
                });
            }
        }
    }

    private static async Task EnsureUserAsync(UserManager<StaffUser> users, ILogger logger, string email, string name, string role, Guid? supplierId)
    {
        if (await users.FindByEmailAsync(email) is not null) return;

        var user = new StaffUser { UserName = email, Email = email, EmailConfirmed = true, FullName = name, SupplierId = supplierId };
        var result = await users.CreateAsync(user, "Admin123!");
        if (!result.Succeeded)
        {
            logger.LogError("Could not seed {Email}: {Errors}", email, string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }
        await users.AddToRoleAsync(user, role);
    }
}
