using System.ComponentModel.DataAnnotations;

namespace EventHub.Web.ViewModels;

public class EventFormViewModel
{
    [Required, StringLength(150, MinimumLength = 3), Display(Name = "Event name")]
    public string Name { get; set; } = "";

    [StringLength(4000)]
    public string? Description { get; set; }

    [Required, StringLength(200)]
    public string Venue { get; set; } = "";

    [Display(Name = "Starts at (UTC)")]
    public DateTime StartsAtUtc { get; set; } = DateTime.UtcNow.Date.AddDays(30).AddHours(18);

    [Range(1, 100000), Display(Name = "Expected guests (venue capacity)")]
    public int ExpectedGuests { get; set; } = 50;

    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Other";

    [Display(Name = "Cover image URL (optional)"), StringLength(500),
     RegularExpression(@"^https?://[^\s'"")]+$", ErrorMessage = "Enter a full http(s) link, or leave it empty.")]
    public string? ImageUrl { get; set; }

    /// <summary>Supply categories ticked on the create form (catering, decoration, sound).</summary>
    public List<string> Needs { get; set; } = [];
}

public class RequirementViewModel
{
    [Required]
    public string Category { get; set; } = "";

    [StringLength(500)]
    public string? Notes { get; set; }
}

public class OrderItemInput
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}

public class PlaceOrderViewModel
{
    public Guid EventId { get; set; }
    public Guid SupplierId { get; set; }

    [Display(Name = "Deliver at (UTC)")]
    public DateTime DeliveryAtUtc { get; set; }

    [Required, StringLength(300), Display(Name = "Delivery address")]
    public string DeliveryAddress { get; set; } = "";

    [StringLength(1000)]
    public string? Notes { get; set; }

    public List<OrderItemInput> Items { get; set; } = [];
}

public class TicketTypeViewModel
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [StringLength(300)]
    public string? Description { get; set; }

    [Range(0, 100000)]
    public decimal Price { get; set; }

    [Range(1, 100000)]
    public int Quantity { get; set; } = 100;
}

public class CheckoutViewModel
{
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }

    [Range(1, 10)]
    public int Quantity { get; set; } = 1;

    [Required, StringLength(100), Display(Name = "Name on card")]
    public string CardHolder { get; set; } = "";

    [Required, StringLength(25), Display(Name = "Card number")]
    public string CardNumber { get; set; } = "";

    [Required, StringLength(7), Display(Name = "Expiry (MM/YY)")]
    public string Expiry { get; set; } = "";

    [Required, StringLength(4, MinimumLength = 3), Display(Name = "Security code")]
    public string Cvv { get; set; } = "";
}

public class PayInvoiceViewModel
{
    [Required, StringLength(100), Display(Name = "Name on card")]
    public string CardHolder { get; set; } = "";

    [Required, StringLength(25), Display(Name = "Card number")]
    public string CardNumber { get; set; } = "";

    [Required, StringLength(7), Display(Name = "Expiry (MM/YY)")]
    public string Expiry { get; set; } = "";

    [Required, StringLength(4, MinimumLength = 3), Display(Name = "Security code")]
    public string Cvv { get; set; } = "";
}
