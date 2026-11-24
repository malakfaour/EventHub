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

    [Range(1, 100000), Display(Name = "Expected guests")]
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

/// <summary>Filters and sort order for the public Discover page. Every value comes from the query string.</summary>
public sealed class DiscoverQuery
{
    public const string WhenToday = "today", WhenWeek = "week", WhenMonth = "month";
    public const string PriceFree = "free", PricePaid = "paid";
    public const string SortDate = "date", SortPopular = "popular", SortPrice = "price";

    [StringLength(100)]
    public string? Search { get; set; }
    public string? Type { get; set; }
    /// <summary>today, week (next 7 days), month (next 30 days) or empty for any date.</summary>
    public string? When { get; set; }
    /// <summary>free (has a free ticket type), paid (every ticket type has a price) or empty.</summary>
    public string? Price { get; set; }
    /// <summary>date (soonest first, default), popular (most tickets sold) or price (cheapest first).</summary>
    public string? Sort { get; set; }

    public bool HasFilters => !string.IsNullOrWhiteSpace(Search) || !string.IsNullOrWhiteSpace(Type)
                              || !string.IsNullOrWhiteSpace(When) || !string.IsNullOrWhiteSpace(Price);
}

/// <summary>Headline numbers for the Discover hero, counted over every upcoming published event.</summary>
public sealed record PublicStats(int UpcomingEvents, int TicketsSold, int EventTypes);

public sealed class DiscoverViewModel
{
    public DiscoverQuery Query { get; init; } = new();
    public List<EventHub.Web.Models.Event> Events { get; init; } = [];
    public PublicStats Stats { get; init; } = new(0, 0, 0);
}

/// <summary>The public Contact form. Website is a honeypot: people never see it, bots fill it in.</summary>
public sealed class ContactFormViewModel
{
    [Required(ErrorMessage = "Tell us your name."), StringLength(100), Display(Name = "Your name")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "We need an email to reply to."), EmailAddress(ErrorMessage = "Enter a valid email address."), StringLength(200)]
    public string Email { get; set; } = "";

    [Required, Display(Name = "What is it about?")]
    public string Topic { get; set; } = EventHub.Web.Models.ContactTopics.Tickets;

    [Required(ErrorMessage = "Write your message."), StringLength(4000, MinimumLength = 10, ErrorMessage = "Write at least 10 characters."), Display(Name = "Message")]
    public string Message { get; set; } = "";

    public string? Website { get; set; }
}

/// <summary>Real numbers for the About page.</summary>
public sealed record AboutStats(int UpcomingEvents, int TicketsSold, int Suppliers, int Organizers);
