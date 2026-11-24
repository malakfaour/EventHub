using System.ComponentModel.DataAnnotations;

namespace SupplierSystem.Web.ViewModels;

/// <summary>An order a staff member records by hand (phone call, walk-in customer).</summary>
public class ManualOrderViewModel
{
    [Required, StringLength(100), Display(Name = "Customer name")]
    public string CustomerName { get; set; } = "";

    [EmailAddress, StringLength(200), Display(Name = "Customer email")]
    public string? CustomerEmail { get; set; }

    [Required, StringLength(150), Display(Name = "Event or occasion")]
    public string EventName { get; set; } = "";

    [Required, Display(Name = "Delivery (UTC)")]
    public DateTime DeliveryAtUtc { get; set; } = DateTime.UtcNow.Date.AddDays(7).AddHours(12);

    [Required, StringLength(300), Display(Name = "Deliver to")]
    public string DeliveryAddress { get; set; } = "";

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<ManualOrderItem> Items { get; set; } = [];
}

public class ManualOrderItem
{
    public Guid ProductId { get; set; }

    [Range(0, 100000)]
    public int Quantity { get; set; }
}
