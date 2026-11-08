using EventHub.Admin.Models;
using EventHub.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Admin.Data;

/// <summary>EventHubAdminDb. Filled only by consumed messages and by staff actions.</summary>
public class AppDbContext : IdentityDbContext<StaffUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ModerationItem> ModerationItems => Set<ModerationItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<MessageLogRow> MessageLog => Set<MessageLogRow>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureMessaging();

        builder.Entity<StaffUser>(e => e.Property(x => x.FullName).HasMaxLength(100));

        builder.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasData(SeedData.Categories.Select(c => new Category { Id = c.Id, Name = c.Name, IsActive = true }));
        });

        builder.Entity<ModerationItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Location).HasMaxLength(200);
            e.Property(x => x.DecisionReason).HasMaxLength(1000);
            e.Property(x => x.AiExplanation).HasMaxLength(1000);
            e.Property(x => x.TicketPrice).HasPrecision(10, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.EventId).IsUnique();   // a second EventSubmitted for the same event is ignored
            e.HasIndex(x => x.Status);
        });
    }
}
