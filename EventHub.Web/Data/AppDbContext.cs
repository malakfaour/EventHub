using EventHub.Contracts;
using EventHub.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Web.Data;

/// <summary>EventHubDb. The Supplier System never reads it; the only link between the two apps is RabbitMQ.</summary>
public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventRequirement> Requirements => Set<EventRequirement>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<TicketPurchase> Purchases => Set<TicketPurchase>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<CatalogSupplier> CatalogSuppliers => Set<CatalogSupplier>();
    public DbSet<CatalogProduct> CatalogProducts => Set<CatalogProduct>();
    public DbSet<SupplyOrder> Orders => Set<SupplyOrder>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<SupplyOrderItem> OrderItems => Set<SupplyOrderItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<MessageLogRow> MessageLog => Set<MessageLogRow>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureMessaging();

        builder.Entity<AppUser>(e =>
        {
            e.Property(x => x.DisplayName).HasMaxLength(100);
            e.Property(x => x.Organization).HasMaxLength(150);
        });

        builder.Entity<Event>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.Venue).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.EventType).HasMaxLength(30);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
            e.HasMany(x => x.TicketTypes).WithOne().HasForeignKey(t => t.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.IsPublished);
            e.HasMany(x => x.Requirements).WithOne().HasForeignKey(r => r.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Guests).WithOne().HasForeignKey(g => g.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.OrganizerId);   // plain column: the organizer is this app's own user, but no navigation is needed
        });

        builder.Entity<EventRequirement>(e =>
        {
            e.Property(x => x.Category).HasMaxLength(30);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => new { x.EventId, x.Category }).IsUnique();   // one requirement per category per event
        });

        builder.Entity<Guest>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Email).HasMaxLength(200);
        });

        builder.Entity<TicketType>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.Remaining);
        });

        builder.Entity<TicketPurchase>(e =>
        {
            e.Property(x => x.TicketTypeName).HasMaxLength(100);
            e.Property(x => x.UnitPrice).HasPrecision(10, 2);
            e.Property(x => x.Total).HasPrecision(12, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PaymentReference).HasMaxLength(60);
            e.Property(x => x.FailureReason).HasMaxLength(300);
            e.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Tickets).WithOne().HasForeignKey(t => t.PurchaseId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ClientId);   // plain columns: a client is just a user id here, no navigation needed
            e.HasIndex(x => x.EventId);
        });

        builder.Entity<Ticket>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(30);
            e.Property(x => x.HolderName).HasMaxLength(120);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.EventId);
        });

        builder.Entity<CatalogSupplier>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Category).HasMaxLength(30);
            e.HasMany(x => x.Products).WithOne().HasForeignKey(p => p.SupplierId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Category);
        });

        builder.Entity<CatalogProduct>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Unit).HasMaxLength(30);
            e.Property(x => x.Price).HasPrecision(12, 2);
        });

        builder.Entity<SupplyOrder>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();   // the id travels in the messages
            e.Property(x => x.SupplierName).HasMaxLength(150);
            e.Property(x => x.Category).HasMaxLength(30);
            e.Property(x => x.DeliveryAddress).HasMaxLength(300);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.StatusNote).HasMaxLength(1000);
            e.Property(x => x.Total).HasPrecision(12, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Event).WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.EventId);
            e.HasIndex(x => x.OrganizerId);
        });

        builder.Entity<SupplyOrderItem>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Unit).HasMaxLength(30);
            e.Property(x => x.UnitPrice).HasPrecision(12, 2);
            e.Ignore(x => x.LineTotal);
        });

        builder.Entity<Invoice>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();   // the supplier's invoice id
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PaymentReference).HasMaxLength(60);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Ignore(x => x.IsOverdue);
            e.Ignore(x => x.Number);
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.OrderId).IsUnique();
        });
    }
}

