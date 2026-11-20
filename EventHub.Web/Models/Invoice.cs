namespace EventHub.Web.Models;

public enum InvoiceStatus { Due, Paid }

/// <summary>A supplier's bill for a delivered order. Created from the InvoiceIssued message, paid by the organizer.</summary>
public class Invoice
{
    public Guid Id { get; set; }                  // the supplier's invoice id (not generated here)
    public Guid OrderId { get; set; }
    public SupplyOrder? Order { get; set; }
    public Guid SupplierId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime IssuedAtUtc { get; set; }
    public DateTime DueAtUtc { get; set; }
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Due;
    public DateTime? PaidAtUtc { get; private set; }
    public string? PaymentReference { get; private set; }

    /// <summary>Guards against two clicks paying the same invoice twice.</summary>
    public byte[] RowVersion { get; set; } = [];

    public bool IsOverdue => Status == InvoiceStatus.Due && DueAtUtc < DateTime.UtcNow;
    public string Number => $"INV-{Id.ToString("N")[..8].ToUpperInvariant()}";

    public void MarkPaid(string reference)
    {
        if (Status == InvoiceStatus.Paid) throw new InvalidOperationException("This invoice is already paid.");
        Status = InvoiceStatus.Paid;
        PaidAtUtc = DateTime.UtcNow;
        PaymentReference = reference;
    }
}
