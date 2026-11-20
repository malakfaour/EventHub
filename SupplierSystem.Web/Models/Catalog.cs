namespace SupplierSystem.Web.Models;

/// <summary>A supplier company. Each supplier has exactly one specialty (see SupplyCategories).</summary>
public class Supplier
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string ContactEmail { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<Product> Products { get; set; } = [];
}

/// <summary>A product or service in one supplier's catalog. It always belongs to the supplier's own category.</summary>
public class Product
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "piece";
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public InventoryItem? Inventory { get; set; }
}

/// <summary>Stock of one product. Reserved stock is promised to accepted orders and leaves on dispatch.</summary>
public class InventoryItem
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public int QuantityOnHand { get; set; }
    public int ReservedQuantity { get; set; }
    public int MinLevel { get; set; }

    public int Available => QuantityOnHand - ReservedQuantity;
    public bool IsLow => Available <= MinLevel;
}

/// <summary>An organizer known through received orders. ExternalOrganizerId is the Event Hub's id (no foreign key).</summary>
public class Customer
{
    public Guid Id { get; set; }
    public Guid ExternalOrganizerId { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
}
