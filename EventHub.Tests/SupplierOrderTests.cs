using EventHub.Contracts;
using SupplierSystem.Web.Models;

namespace EventHub.Tests;

public class SupplierOrderTests
{
    private static SupplyOrder NewOrder() =>
        SupplyOrder.CreateManual(Guid.NewGuid(), Guid.NewGuid(), "Party", DateTime.UtcNow.AddDays(3), "Main street", null,
            [(Guid.NewGuid(), "Chairs", "pcs", 10, 2.5m), (Guid.NewGuid(), "Tables", "pcs", 2, 20m)]);

    [Fact]
    public void Manual_order_total_is_the_sum_of_its_lines()
    {
        var order = NewOrder();
        Assert.Equal(65m, order.Total);
        Assert.Equal(OrderSource.Manual, order.Source);
        Assert.Null(order.ExternalOrderId);
    }

    [Fact]
    public void Manual_order_needs_at_least_one_item()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SupplyOrder.CreateManual(Guid.NewGuid(), Guid.NewGuid(), "Party", DateTime.UtcNow, "x", null, []));
    }

    [Fact]
    public void Order_moves_forward_through_every_step()
    {
        var order = NewOrder();
        order.Accept();
        order.StartPreparing();
        order.Dispatch();
        order.Deliver();
        Assert.Equal(SupplyOrderStatus.Delivered, order.Status);
    }

    [Fact]
    public void Steps_cannot_be_skipped()
    {
        var order = NewOrder();
        Assert.Throws<InvalidOperationException>(() => order.Dispatch());
        Assert.Throws<InvalidOperationException>(() => order.StartPreparing());
    }

    [Fact]
    public void Rejected_order_cannot_be_accepted_later()
    {
        var order = NewOrder();
        order.Reject("No stock");
        Assert.Equal("No stock", order.DecisionNote);
        Assert.Throws<InvalidOperationException>(() => order.Accept());
    }

    [Fact]
    public void Cancelling_is_allowed_until_dispatch()
    {
        var early = NewOrder();
        early.Accept();
        Assert.True(early.HoldsStock);
        early.Cancel("changed plans");
        Assert.Equal(SupplyOrderStatus.Cancelled, early.Status);

        var late = NewOrder();
        late.Accept(); late.StartPreparing(); late.Dispatch();
        Assert.False(late.CanBeCancelled);
        Assert.Throws<InvalidOperationException>(() => late.Cancel("too late"));
    }
}
