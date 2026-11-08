using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EventHub.Client.ViewModels;

public class CreateEventViewModel
{
    [Required, StringLength(120, MinimumLength = 5)]
    public string Title { get; set; } = "";

    [Required, StringLength(4000, MinimumLength = 20)]
    public string Description { get; set; } = "";

    [Display(Name = "Category"), Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
    public int CategoryId { get; set; }

    [Required, Display(Name = "Starts at")]
    public DateTime StartsAt { get; set; } = DateTime.UtcNow.AddDays(7).Date.AddHours(18);

    [Required, StringLength(200)]
    public string Location { get; set; } = "";

    [Range(1, 10000)]
    public int Capacity { get; set; } = 50;

    [Display(Name = "Ticket price (USD, 0 = free)"), Range(0, 10000)]
    public decimal TicketPrice { get; set; }

    public List<SelectListItem> Categories { get; set; } = [];
}
