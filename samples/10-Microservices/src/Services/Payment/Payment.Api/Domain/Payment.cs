// [Payment — Domain] Ödeme kayıtları SADECE Payment servisinin verisidir.
namespace Payment.Api.Domain;

public enum PaymentStatus { Completed, Failed }

public class Payment
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }   // UNIQUE → aynı sipariş iki kez tahsil edilemez
    public Guid CustomerId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime ProcessedAt { get; private set; }

    private Payment() { }

    public static Payment Process(Guid orderId, Guid customerId, decimal amount)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(), OrderId = orderId, CustomerId = customerId, Amount = amount,
            ProcessedAt = DateTime.UtcNow
        };

        // Sahte ödeme sağlayıcısı kuralı: 10.000 TRY üstü reddedilir (compensation'ı tetiklemek için).
        if (amount > PaymentPolicy.SingleTransactionLimit)
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = $"Tutar ({amount:0.00}) tek işlem limitini ({PaymentPolicy.SingleTransactionLimit:0.00}) aşıyor.";
        }
        else
        {
            payment.Status = PaymentStatus.Completed;
        }
        return payment;
    }
}

public static class PaymentPolicy
{
    public const decimal SingleTransactionLimit = 10_000m;
}
