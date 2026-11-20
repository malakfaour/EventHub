using System.ComponentModel.DataAnnotations;

namespace SupplierSystem.Web.ViewModels;

public class ProductViewModel
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [Required, StringLength(30)]
    public string Unit { get; set; } = "piece";

    [Range(0.01, 100000)]
    public decimal Price { get; set; }

    [Display(Name = "In stock"), Range(0, 1000000)]
    public int QuantityOnHand { get; set; }

    [Display(Name = "Low-stock alert at"), Range(0, 1000000)]
    public int MinLevel { get; set; }

    [Display(Name = "Offered to organizers")]
    public bool IsActive { get; set; } = true;
}
