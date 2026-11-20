using EventHub.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SupplierSystem.Web.Models;

namespace SupplierSystem.Web.Data;

/// <summary>SupplierDb. Filled only by consumed messages and by staff actions; the Event Hub never reads it.</summary>
public class AppDbContext : IdentityDbContext<StaffUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryItem> Inventory => Set<InventoryItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SupplyOrder> Orders => Set<SupplyOrder>();
    public DbSet<SupplyOrderLine> OrderLines => Set<SupplyOrderLine>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();
    public DbSet<MessageLogRow> MessageLog => Set<MessageLogRow>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureMessaging();

        builder.Entity<StaffUser>(e => e.Property(x => x.FullName).HasMaxLength(100));

        builder.Entity<Supplier>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Category).HasMaxLength(30);
            e.Property(x => x.ContactEmail).HasMaxLength(200);
            e.HasIndex(x => x.Category);
        });

        builder.Entity<Product>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Unit).HasMaxLength(30);
            e.Property(x => x.Price).HasPrecision(10, 2);
            e.HasOne(x => x.Supplier).WithMany(s => s.Products)
                .HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.SupplierId);
        });

        builder.Entity<InventoryItem>(e =>
        {
            e.ToTable("Inventory");
            e.HasKey(x => x.ProductId);
            e.HasOne(x => x.Product).WithOne(p => p.Inventory)
                .HasForeignKey<InventoryItem>(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Customer>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Email).HasMaxLength(200);
            e.HasIndex(x => x.ExternalOrganizerId).IsUnique();
        });

        builder.Entity<SupplyOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EventName).HasMaxLength(150);
            e.Property(x => x.DeliveryAddress).HasMaxLength(300);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.DecisionNote).HasMaxLength(1000);
            e.Property(x => x.Total).HasPrecision(12, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ExternalOrderId).IsUnique();   // a second SupplyOrderPlaced for the same order is ignored
            e.HasIndex(x => new { x.SupplierId, x.Status });
        });

        builder.Entity<SupplyOrderLine>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Unit).HasMaxLength(30);
            e.Property(x => x.UnitPrice).HasPrecision(10, 2);
        });

        builder.Entity<Invoice>(e =>
        {
            e.Property(x => x.Number).HasMaxLength(30);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PaymentReference).HasMaxLength(60);
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.SupplyOrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.SupplyOrderId).IsUnique();   // one invoice per order
            e.HasIndex(x => new { x.SupplierId, x.Status });
        });
    }
}
