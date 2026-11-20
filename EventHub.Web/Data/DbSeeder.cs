using EventHub.Contracts;
using EventHub.Web.Models;
using EventHub.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Web.Data;

public static class DbSeeder
{
    /// <summary>Applies migrations (waiting for SQL Server to come up) and seeds a demo organizer in Development.</summary>
    public static async Task MigrateAndSeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var db = sp.GetRequiredService<AppDbContext>();

        if (!db.Database.GetMigrations().Any())
            throw new InvalidOperationException(
                "No EF Core migrations found. Run: dotnet ef migrations add Initial --project EventHub.Web --output-dir Data/Migrations");

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

        // Events created before event types existed get the default type.
        await db.Events.Where(e => e.EventType == "").ExecuteUpdateAsync(u => u.SetProperty(e => e.EventType, "Other"));

        // Roles exist everywhere. Accounts created before roles were introduced were all organizers.
        var roles = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in Roles.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole<Guid>(role));

        var users = sp.GetRequiredService<UserManager<AppUser>>();
        foreach (var existing in await users.Users.ToListAsync())
            if ((await users.GetRolesAsync(existing)).Count == 0)
                await users.AddToRoleAsync(existing, Roles.Organizer);

        if (!sp.GetRequiredService<IHostEnvironment>().IsDevelopment()) return;

        const string email = "organizer@eventhub.local";
        if (await users.FindByEmailAsync(email) is null)
        {
            var result = await users.CreateAsync(
                new AppUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Demo Organizer", Organization = "Demo Events Ltd" },
                "Organizer123!");
            if (!result.Succeeded)
                logger.LogError("Could not seed demo organizer: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
            else
                await users.AddToRoleAsync((await users.FindByEmailAsync(email))!, Roles.Organizer);
        }

        // Demo events (no orders): the organizer places those through the app to try the round trip.
        var organizer = await users.FindByEmailAsync(email);
        if (organizer is not null && !await db.Events.AnyAsync(e => e.OrganizerId == organizer.Id))
        {
            Event Make(string name, string venue, int days, int hour, int guests, string description, params (string Category, string Notes)[] needs)
            {
                var ev = new Event
                {
                    Id = Guid.NewGuid(), OrganizerId = organizer.Id, Name = name, Venue = venue, ExpectedGuests = guests,
                    Description = description, StartsAtUtc = DateTime.UtcNow.Date.AddDays(days).AddHours(hour)
                };
                foreach (var n in needs)
                    ev.Requirements.Add(new EventRequirement { Id = Guid.NewGuid(), EventId = ev.Id, Category = n.Category, Notes = n.Notes });
                for (var i = 1; i <= Math.Min(guests, 5); i++)
                    ev.Guests.Add(new Guest { Id = Guid.NewGuid(), EventId = ev.Id, Name = $"Guest {i}", Email = $"guest{i}@example.com" });
                return ev;
            }

            db.Events.AddRange(
                Make("Sara & Omar Wedding", "Grand Hall, Beirut Waterfront", 45, 18, 150, "Evening wedding reception with dinner and live music.",
                    (SupplyCategories.Catering, "Gala dinner, no nuts"), (SupplyCategories.Decoration, "White and gold theme"), (SupplyCategories.Sound, "DJ and microphones")),
                Make("Tech Conference 2027", "City Convention Center, Hall B", 90, 9, 300, "One-day conference with talks and coffee breaks.",
                    (SupplyCategories.Catering, "Coffee breaks and lunch"), (SupplyCategories.Sound, "PA system and wireless mics")),
                Make("Company Anniversary Party", "Rooftop Lounge, Hamra", 20, 19, 80, "Team party for the 10th anniversary.",
                    (SupplyCategories.Catering, "Finger food and drinks"), (SupplyCategories.Decoration, "Balloons and centerpieces")));
            await db.SaveChangesAsync();
        }

        // Demo orders. They are saved together with their outbox messages, exactly like an order placed in the app,
        // so the Supplier System receives them as new orders when it is running.
        if (organizer is not null && !await db.Orders.AnyAsync())
        {
            var outbox = sp.GetRequiredService<IOutbox>();

            async Task AddOrderAsync(string eventName, DemoSupplier supplier, int hoursBefore, params (int Product, int Qty)[] lines)
            {
                var ev = await db.Events.FirstOrDefaultAsync(e => e.OrganizerId == organizer.Id && e.Name == eventName);
                if (ev is null) return;

                var orderId = Guid.NewGuid();
                var items = lines.Select(l =>
                {
                    var p = supplier.Products[l.Product];
                    return new SupplyOrderItem { Id = Guid.NewGuid(), OrderId = orderId, ProductId = p.Id, Name = p.Name, Unit = p.Unit, Quantity = l.Qty, UnitPrice = p.Price };
                }).ToList();

                var order = new SupplyOrder
                {
                    Id = orderId, EventId = ev.Id, OrganizerId = organizer.Id,
                    SupplierId = supplier.Id, SupplierName = supplier.Name, Category = supplier.Category,
                    DeliveryAtUtc = ev.StartsAtUtc.AddHours(-hoursBefore), DeliveryAddress = ev.Venue,
                    Items = items, Total = items.Sum(i => i.LineTotal)
                };
                db.Orders.Add(order);
                outbox.Enqueue(new SupplyOrderPlaced(order.Id, ev.Id, ev.Name, organizer.Id, organizer.DisplayName, organizer.Email ?? "",
                    supplier.Id, supplier.Category, order.DeliveryAtUtc, order.DeliveryAddress, null,
                    items.Select(i => new OrderLine(i.ProductId, i.Name, i.Unit, i.Quantity, i.UnitPrice)).ToList())
                { CorrelationId = order.Id });
            }

            await AddOrderAsync("Company Anniversary Party", DemoData.RoyalFeast, 3, (2, 80));
            await AddOrderAsync("Company Anniversary Party", DemoData.BloomCo, 5, (2, 1), (0, 10));
            await AddOrderAsync("Tech Conference 2027", DemoData.GoldenFork, 4, (0, 300), (2, 300));
            await AddOrderAsync("Tech Conference 2027", DemoData.SoundWave, 6, (0, 1), (1, 6));
            await db.SaveChangesAsync();
        }

        // Demo client and admin accounts.
        async Task EnsureAsync(string mail, string name, string password, string role)
        {
            if (await users.FindByEmailAsync(mail) is not null) return;
            var created = new AppUser { UserName = mail, Email = mail, EmailConfirmed = true, DisplayName = name };
            var made = await users.CreateAsync(created, password);
            if (made.Succeeded) await users.AddToRoleAsync(created, role);
            else logger.LogError("Could not seed {Email}: {Errors}", mail, string.Join("; ", made.Errors.Select(e => e.Description)));
        }
        await EnsureAsync("client@eventhub.local", "Demo Client", "Client123!", Roles.Client);
        await EnsureAsync("admin@eventhub.local", "Platform Admin", "Admin123!", Roles.Admin);

        // Public events with tickets, so the Discover page is not empty.
        if (organizer is not null && !await db.TicketTypes.AnyAsync())
        {
            void AddTypes(Event ev, params (string Name, string? Description, decimal Price, int Quantity)[] types)
            {
                foreach (var t in types)
                    db.TicketTypes.Add(new TicketType { Id = Guid.NewGuid(), EventId = ev.Id, Name = t.Name, Description = t.Description, Price = t.Price, Quantity = t.Quantity });
            }

            Event NewPublic(string name, string type, string venue, int days, int hour, string description, params (string Name, string? Description, decimal Price, int Quantity)[] types)
            {
                var ev = new Event
                {
                    Id = Guid.NewGuid(), OrganizerId = organizer.Id, Name = name, EventType = type, Venue = venue,
                    Description = description, StartsAtUtc = DateTime.UtcNow.Date.AddDays(days).AddHours(hour), ExpectedGuests = types.Sum(t => t.Quantity)
                };
                db.Events.Add(ev);
                AddTypes(ev, types);
                return ev;
            }

            var tech = await db.Events.FirstOrDefaultAsync(e => e.OrganizerId == organizer.Id && e.Name == "Tech Conference 2027");
            if (tech is not null)
            {
                tech.EventType = "Conference";
                AddTypes(tech, ("Early bird", "Limited, while it lasts", 49m, 100), ("Standard", "Full day access", 79m, 250), ("VIP", "Front rows and speaker dinner", 149m, 50));
            }

            var jazz = NewPublic("Beirut Jazz Night", "Music", "Zaitunay Bay, Beirut", 12, 20, "An open-air evening of live jazz by the sea with three bands and a late jam session.",
                ("General", "Standing area", 25m, 200), ("VIP", "Seated, front of stage, welcome drink", 60m, 12));
            var food = NewPublic("Food & Wine Festival", "Food & Drink", "Biel, Beirut", 25, 17, "Taste dishes from 40 local chefs and wines from across the region.",
                ("Day pass", "Entry and festival map", 18m, 500), ("Tasting pass", "Entry plus 10 tasting tokens", 45m, 60));
            var pitch = NewPublic("Startup Pitch Night", "Conference", "Beirut Digital District", 18, 18, "Ten founders, five minutes each, one winner. Meet investors afterwards.",
                ("Free entry", null, 0m, 150), ("Investor pass", "Includes the networking dinner", 30m, 30));
            var roof = NewPublic("Summer Rooftop Party", "Party", "Skybar Rooftop, Gemmayzeh", 35, 21, "DJs, cocktails and a view of the whole city. Dress to impress.",
                ("Early bird", "First 50 only", 12m, 50), ("Standard", null, 20m, 250));
            var match = NewPublic("Football Charity Match", "Sports", "Camille Chamoun Stadium", 40, 16, "Local legends against the city's best amateurs. All profits go to children's education.",
                ("Stand", null, 10m, 2000), ("Premium", "Covered seats near the pitch", 35m, 300));

            await db.SaveChangesAsync();   // events and ticket types first, then publish (publishing needs the ticket types)

            foreach (var ev in new[] { jazz, food, pitch, roof, match, tech })
            {
                if (ev is null) continue;
                var loaded = await db.Events.Include(e => e.TicketTypes).FirstAsync(e => e.Id == ev.Id);
                if (loaded.Status == EventStatus.Draft) loaded.Plan();
                loaded.Publish();
            }
            await db.SaveChangesAsync();
        }
    }
}
