namespace EventHub.Web.Services;

public sealed record PaymentRequest(decimal Amount, string Currency, string CardHolder, string CardNumber, string Expiry, string Cvv, string Description);

public sealed record PaymentResult(bool Succeeded, string? Reference, string? Error);

/// <summary>The payment provider as the rest of the app sees it. A real provider would be plugged in behind this interface.</summary>
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct = default);
}

/// <summary>
/// A pretend gateway for the demo. It checks the card fields like a real one would and returns a payment reference.
/// Card ending 0000 is declined, 9999 has insufficient funds, anything else succeeds. Card data is never stored or logged.
/// </summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly ILogger<FakePaymentGateway> _logger;

    public FakePaymentGateway(ILogger<FakePaymentGateway> logger) => _logger = logger;

    public async Task<PaymentResult> ChargeAsync(PaymentRequest r, CancellationToken ct = default)
    {
        await Task.Delay(600, ct); // pretend to talk to a bank

        var digits = new string(r.CardNumber.Where(char.IsDigit).ToArray());
        if (digits.Length is < 13 or > 19) return Fail("Card number is not valid.");
        if (!r.Cvv.All(char.IsDigit) || r.Cvv.Length is < 3 or > 4) return Fail("Security code is not valid.");
        if (!ExpiryIsValid(r.Expiry)) return Fail("The card has expired or the date is not valid (use MM/YY).");

        var last4 = digits[^4..];
        if (last4 == "0000") return Fail("Your card was declined.");
        if (last4 == "9999") return Fail("Insufficient funds.");

        var reference = $"PAY-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";
        _logger.LogInformation("Fake gateway approved {Amount} {Currency} on card ending {Last4}: {Reference}", r.Amount, r.Currency, last4, reference);
        return new PaymentResult(true, reference, null);

        PaymentResult Fail(string error)
        {
            _logger.LogInformation("Fake gateway refused a payment: {Error}", error);
            return new PaymentResult(false, null, error);
        }
    }

    private static bool ExpiryIsValid(string expiry)
    {
        var parts = expiry.Split('/');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var month) || !int.TryParse(parts[1], out var year)) return false;
        if (month is < 1 or > 12) return false;
        if (year < 100) year += 2000;
        var lastDay = new DateTime(year, month, 1).AddMonths(1).AddDays(-1);
        return lastDay >= DateTime.UtcNow.Date;
    }
}
