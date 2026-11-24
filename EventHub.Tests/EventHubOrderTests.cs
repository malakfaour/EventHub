using EventHub.Contracts;
using EventHub.Web.Models;

namespace EventHub.Tests;

public class EventHubOrderTests
{
    [Fact]
    public void Status_updates_only_move_forward()
    {
        var order = new SupplyOrder();
        Assert.True(order.ApplyStatus(SupplyOrderStatus.Accepted, null));
        Assert.False(order.ApplyStatus(SupplyOrderStatus.Received, null));   // an old message arriving late
        Assert.False(order.ApplyStatus(SupplyOrderStatus.Accepted, null));   // a duplicate
        Assert.Equal(SupplyOrderStatus.Accepted, order.Status);
    }

    [Fact]
    public void Nothing_changes_after_delivery()
    {
        var order = new SupplyOrder();
        order.ApplyStatus(SupplyOrderStatus.Delivered, null);
        Assert.False(order.ApplyStatus(SupplyOrderStatus.Dispatched, null));
        Assert.False(order.ApplyRejected("late"));
    }

    [Fact]
    public void Rejection_only_works_before_acceptance()
    {
        var order = new SupplyOrder();
        Assert.True(order.ApplyRejected("No stock"));
        Assert.Equal(SupplyOrderStatus.Rejected, order.Status);
        Assert.False(order.IsActive);

        var accepted = new SupplyOrder();
        accepted.ApplyStatus(SupplyOrderStatus.Accepted, null);
        Assert.False(accepted.ApplyRejected("too late"));
    }

    [Fact]
    public void Dispatched_order_cannot_be_cancelled()
    {
        var order = new SupplyOrder();
        order.ApplyStatus(SupplyOrderStatus.Dispatched, null);
        Assert.Throws<InvalidOperationException>(() => order.Cancel("no"));
    }

    [Fact]
    public void Cancelled_order_no_longer_counts_as_active()
    {
        var order = new SupplyOrder();
        order.Cancel("changed my mind");
        Assert.False(order.IsActive);
    }
}
