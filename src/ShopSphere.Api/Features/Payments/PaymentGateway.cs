namespace ShopSphere.Api.Features.Payments;

public record PaymentCharge(string Reference, decimal Amount, string PaymentToken);

public record PaymentResult(bool Succeeded, string TransactionReference, string? FailureReason);

public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(PaymentCharge charge, CancellationToken cancellationToken = default);
}

// Stands in for a real provider. The client sends a token, never card data,
// which is how a real integration (Stripe, Adyen) works as well.
public class MockPaymentGateway : IPaymentGateway
{
    public const string Provider = "Mock";

    public const string SuccessToken = "tok_success";
    public const string DeclinedToken = "tok_declined";
    public const string InsufficientFundsToken = "tok_insufficient_funds";

    private readonly ILogger<MockPaymentGateway> _logger;

    public MockPaymentGateway(ILogger<MockPaymentGateway> logger)
    {
        _logger = logger;
    }

    public async Task<PaymentResult> ChargeAsync(PaymentCharge charge, CancellationToken cancellationToken = default)
    {
        // Pretend we are calling an external service
        await Task.Delay(200, cancellationToken);

        var transactionReference = $"mock_{Guid.NewGuid():N}"[..20];

        var result = charge.PaymentToken switch
        {
            DeclinedToken => new PaymentResult(false, transactionReference, "The card was declined."),
            InsufficientFundsToken => new PaymentResult(false, transactionReference, "Insufficient funds."),
            _ => new PaymentResult(true, transactionReference, null)
        };

        _logger.LogInformation("Mock payment for {Reference} of {Amount}: {Outcome}",
            charge.Reference, charge.Amount, result.Succeeded ? "succeeded" : "failed");

        return result;
    }
}
