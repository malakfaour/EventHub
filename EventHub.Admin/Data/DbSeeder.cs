using EventHub.Admin.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Admin.Data;

public static class DbSeeder
{
    /// <summary>Applies migrations (waiting for SQL Server) and seeds roles plus demo staff accounts in Development.</summary>
    public static async Task MigrateAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var db = sp.GetRequiredService<AppDbContext>();

        if (!db.Database.GetMigrations().Any())
            throw new InvalidOperationException(
                "No EF Core migrations found. Run: dotnet ef migrations add Initial --project EventHub.Admin --output-dir Data/Migrations");

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync();
                break;
            }
            catch (Exception ex) when (attempt < 20)
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

        var users = sp.GetRequiredService<UserManager<StaffUser>>();
        await EnsureUserAsync(users, logger, "admin@eventhub.local", "Demo Admin", Roles.Admin);
        await EnsureUserAsync(users, logger, "moderator@eventhub.local", "Demo Moderator", Roles.Moderator);
        await EnsureUserAsync(users, logger, "analyst@eventhub.local", "Demo Analyst", Roles.Analyst);
    }

    private static async Task EnsureUserAsync(UserManager<StaffUser> users, ILogger logger, string email, string name, string role)
    {
        if (await users.FindByEmailAsync(email) is not null) return;

        var user = new StaffUser { UserName = email, Email = email, EmailConfirmed = true, FullName = name };
        var result = await users.CreateAsync(user, "Admin123!");
        if (!result.Succeeded)
        {
            logger.LogError("Could not seed {Email}: {Errors}", email, string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }
        await users.AddToRoleAsync(user, role);
    }
}
