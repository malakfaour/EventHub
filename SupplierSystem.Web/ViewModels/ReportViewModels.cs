using EventHub.Contracts;
using SupplierSystem.Web.Models;

namespace SupplierSystem.Web.ViewModels;

public record LabelValue(string Label, decimal Value);

public record ReportOrderRow(Guid Id, string SupplierName, string EventName, string Customer, OrderSource Source,
    SupplyOrderStatus Status, decimal Total, DateTime ReceivedAtUtc, DateTime DeliveryAtUtc);

public record ReportLineRow(string Product, SupplyOrderStatus Status, int Quantity, decimal LineTotal);

public record ReportInvoiceRow(string Number, string EventName, decimal Amount, InvoiceStatus Status, DateTime IssuedAtUtc, DateTime DueAtUtc, DateTime? PaidAtUtc);

/// <summary>Everything the supplier dashboard charts and the exports are built from.</summary>
public class SupplierReport
{
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string Scope { get; set; } = "";
    public int Orders { get; set; }
    public int Delivered { get; set; }
    public decimal Revenue { get; set; }            // delivered orders
    public decimal AcceptanceRate { get; set; }     // accepted-or-later / (accepted-or-later + rejected), percent
    public decimal Invoiced { get; set; }
    public decimal Collected { get; set; }
    public decimal Outstanding { get; set; }
    public List<LabelValue> OrdersByMonth { get; set; } = [];
    public List<LabelValue> OrdersByStatus { get; set; } = [];
    public List<LabelValue> RevenueByProduct { get; set; } = [];
    public List<LabelValue> InvoiceAmounts { get; set; } = [];
    public List<ReportOrderRow> OrderRows { get; set; } = [];
    public List<ReportInvoiceRow> InvoiceRows { get; set; } = [];
}
