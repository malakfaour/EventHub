using EventHub.Client.Models;
using EventHub.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Client.Data;

/// <summary>EventHubClientDb. Never read by the Admin App; the only link between the two is RabbitMQ.</summary>
public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<MessageLogRow> MessageLog => Set<MessageLogRow>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureMessaging();

        builder.Entity<AppUser>(e => e.Property(x => x.DisplayName).HasMaxLength(100));

        builder.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.HasData(SeedData.Categories.Select(c => new Category { Id = c.Id, Name = c.Name, IsActive = true }));
        });

        builder.Entity<Event>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Location).HasMaxLength(200);
            e.Property(x => x.RejectionReason).HasMaxLength(1000);
            e.Property(x => x.TicketPrice).HasPrecision(10, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasIndex(x => x.OrganizerId);
            e.HasIndex(x => x.Status);
            // OrganizerId and CategoryId are plain columns here; cross-app links are never foreign keys.
        });
    }
}
