using EventHub.Client.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Client.Data;

public static class DbSeeder
{
    /// <summary>Applies migrations (waiting for SQL Server to come up) and seeds a demo member in Development.</summary>
    public static async Task MigrateAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var db = sp.GetRequiredService<AppDbContext>();

        if (!db.Database.GetMigrations().Any())
            throw new InvalidOperationException(
                "No EF Core migrations found. Run: dotnet ef migrations add Initial --project EventHub.Client --output-dir Data/Migrations");

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

        if (!sp.GetRequiredService<IHostEnvironment>().IsDevelopment()) return;

        var users = sp.GetRequiredService<UserManager<AppUser>>();
        const string email = "member@eventhub.local";
        if (await users.FindByEmailAsync(email) is null)
        {
            var result = await users.CreateAsync(
                new AppUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Demo Member" },
                "Member123!");
            if (!result.Succeeded)
                logger.LogError("Could not seed demo member: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
