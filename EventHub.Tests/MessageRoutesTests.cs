using EventHub.Contracts;

namespace EventHub.Tests;

public class MessageRoutesTests
{
    [Fact]
    public void Every_registered_message_type_has_a_route()
    {
        foreach (var type in MessageRoutes.AllTypes)
        {
            var route = MessageRoutes.For(type);
            Assert.False(string.IsNullOrWhiteSpace(route.Exchange));
            Assert.False(string.IsNullOrWhiteSpace(route.RoutingKey));
        }
    }

    [Fact]
    public void Order_messages_go_to_the_event_hub_exchange_with_the_category_in_the_key()
    {
        var placed = new SupplyOrderPlaced(Guid.NewGuid(), Guid.NewGuid(), "Gala", Guid.NewGuid(), "Org", "o@x.com",
            Guid.NewGuid(), SupplyCategories.Catering, DateTime.UtcNow, "Hall", null, []);

        var route = MessageRoutes.For(placed);

        Assert.Equal(Topology.EventHubExchange, route.Exchange);
        Assert.EndsWith("." + SupplyCategories.Catering, route.RoutingKey);
    }

    [Fact]
    public void Supplier_messages_use_the_supplier_exchange()
    {
        Assert.Equal(Topology.SupplierExchange, MessageRoutes.For<InvoiceIssued>().Exchange);
        Assert.Equal(Topology.SupplierExchange, MessageRoutes.For<OrderStatusChanged>().Exchange);
    }

    [Fact]
    public void Resolve_finds_a_type_by_its_short_name()
    {
        Assert.Equal(typeof(PaymentCompleted), MessageRoutes.Resolve(nameof(PaymentCompleted)));
        Assert.Null(MessageRoutes.Resolve("DoesNotExist"));
    }

    [Fact]
    public void Only_known_categories_are_valid()
    {
        Assert.True(SupplyCategories.IsValid(SupplyCategories.Sound));
        Assert.False(SupplyCategories.IsValid("flowers"));
        Assert.False(SupplyCategories.IsValid(null));
    }
}
