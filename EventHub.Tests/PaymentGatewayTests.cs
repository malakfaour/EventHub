using EventHub.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventHub.Tests;

public class PaymentGatewayTests
{
    private static Task<PaymentResult> Charge(string card, string cvv = "123", string expiry = "12/99") =>
        new FakePaymentGateway(NullLogger<FakePaymentGateway>.Instance)
            .ChargeAsync(new PaymentRequest(10m, "USD", "Test", card, expiry, cvv, "test"));

    [Fact]
    public async Task A_normal_card_is_approved_with_a_reference()
    {
        var result = await Charge("4242 4242 4242 4242");
        Assert.True(result.Succeeded);
        Assert.StartsWith("PAY-", result.Reference);
    }

    [Fact]
    public async Task Card_ending_0000_is_declined()
    {
        var result = await Charge("4242424242420000");
        Assert.False(result.Succeeded);
        Assert.Contains("declined", result.Error);
    }

    [Fact]
    public async Task Card_ending_9999_has_insufficient_funds()
    {
        var result = await Charge("4242424242429999");
        Assert.False(result.Succeeded);
        Assert.Contains("Insufficient", result.Error);
    }

    [Theory]
    [InlineData("1234", "123", "12/99")]        // card number too short
    [InlineData("4242424242424242", "1", "12/99")]   // cvv too short
    [InlineData("4242424242424242", "123", "01/20")] // expired
    [InlineData("4242424242424242", "123", "13/30")] // impossible month
    public async Task Invalid_card_details_are_rejected(string card, string cvv, string expiry)
    {
        var result = await Charge(card, cvv, expiry);
        Assert.False(result.Succeeded);
    }
}
