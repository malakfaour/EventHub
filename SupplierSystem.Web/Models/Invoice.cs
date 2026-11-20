namespace SupplierSystem.Web.Models;

public enum InvoiceStatus { Issued, Paid }

/// <summary>
/// The bill for one delivered order. It is created by staff after delivery, sent to the Event Hub as InvoiceIssued,
/// and marked paid when the Event Hub's PaymentCompleted message arrives.
/// </summary>
public class Invoice
{
    public Guid Id { get; set; }
    public Guid SupplyOrderId { get; set; }
    public SupplyOrder? Order { get; set; }
    public Guid SupplierId { get; set; }
    public string Number { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime DueAtUtc { get; set; }
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Issued;
    public DateTime? PaidAtUtc { get; private set; }
    public string? PaymentReference { get; private set; }

    public bool IsOverdue => Status == InvoiceStatus.Issued && DueAtUtc < DateTime.UtcNow;

    public void MarkPaid(string reference)
    {
        if (Status == InvoiceStatus.Paid) return;
        Status = InvoiceStatus.Paid;
        PaidAtUtc = DateTime.UtcNow;
        PaymentReference = reference;
    }
}
