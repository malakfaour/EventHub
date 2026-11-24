using EventHub.Contracts;

namespace SupplierSystem.Web.Models;

/// <summary>
/// Each supply category has its own ink, shared with the Event Hub poster colors:
/// catering = orange, decoration = pink, sound = blue. Used for chips, swatches and tiles.
/// </summary>
public static class CategoryStyle
{
    public static string Css(string? category) => category switch
    {
        SupplyCategories.Catering => "cat-catering",
        SupplyCategories.Decoration => "cat-decoration",
        SupplyCategories.Sound => "cat-sound",
        _ => "cat-other"
    };

    /// <summary>Status tone for filter pills, matching _StatusBadge.cshtml.</summary>
    public static string Tone(SupplyOrderStatus status) => status switch
    {
        SupplyOrderStatus.Pending or SupplyOrderStatus.Received => "amber",
        SupplyOrderStatus.Accepted => "sky",
        SupplyOrderStatus.Preparing or SupplyOrderStatus.Dispatched => "indigo",
        SupplyOrderStatus.Delivered => "green",
        SupplyOrderStatus.Rejected => "red",
        _ => "gray"
    };
}
