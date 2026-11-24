using EventHub.Web.Models;

namespace EventHub.Tests;

public class InvoiceAndTicketTests
{
    [Fact]
    public void Paying_an_invoice_stores_the_reference()
    {
        var invoice = new Invoice { Id = Guid.NewGuid(), Amount = 50m, DueAtUtc = DateTime.UtcNow.AddDays(5) };
        invoice.MarkPaid("PAY-123");
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal("PAY-123", invoice.PaymentReference);
        Assert.NotNull(invoice.PaidAtUtc);
    }

    [Fact]
    public void An_invoice_cannot_be_paid_twice()
    {
        var invoice = new Invoice { Id = Guid.NewGuid() };
        invoice.MarkPaid("A");
        Assert.Throws<InvalidOperationException>(() => invoice.MarkPaid("B"));
    }

    [Fact]
    public void Only_unpaid_invoices_past_their_due_date_are_overdue()
    {
        var late = new Invoice { Id = Guid.NewGuid(), DueAtUtc = DateTime.UtcNow.AddDays(-1) };
        Assert.True(late.IsOverdue);
        late.MarkPaid("X");
        Assert.False(late.IsOverdue);
        Assert.False(new Invoice { Id = Guid.NewGuid(), DueAtUtc = DateTime.UtcNow.AddDays(3) }.IsOverdue);
    }

    [Fact]
    public void A_ticket_can_be_used_once()
    {
        var ticket = new Ticket { Id = Guid.NewGuid(), Code = "ABC", HolderName = "Sam" };
        Assert.False(ticket.IsUsed);
        ticket.CheckIn();
        Assert.True(ticket.IsUsed);
        Assert.Throws<InvalidOperationException>(() => ticket.CheckIn());
    }

    [Fact]
    public void Refunding_only_changes_paid_purchases()
    {
        var failed = new TicketPurchase { Status = PurchaseStatus.Failed };
        failed.Refund();
        Assert.Equal(PurchaseStatus.Failed, failed.Status);

        var paid = new TicketPurchase { Status = PurchaseStatus.Paid };
        paid.Refund();
        Assert.Equal(PurchaseStatus.Refunded, paid.Status);
    }
}
