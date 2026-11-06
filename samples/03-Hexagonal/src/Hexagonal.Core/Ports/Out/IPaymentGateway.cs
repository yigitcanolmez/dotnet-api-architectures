namespace Hexagonal.Core.Ports.Out;

// [Outbound / Driven Port] Ödeme sağlayıcısı. Driven adapter: Payments/FakePaymentGateway (Stripe vb. olabilir).
public interface IPaymentGateway
{
    Task<PaymentResult> ChargeAsync(Guid customerId, decimal amount, CancellationToken ct = default);
}

public record PaymentResult(bool Success, string? TransactionId, string? Reason)
{
    public static PaymentResult Ok(string transactionId) => new(true, transactionId, null);
    public static PaymentResult Declined(string reason) => new(false, null, reason);
}
