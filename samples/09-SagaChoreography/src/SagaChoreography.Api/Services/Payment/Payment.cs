namespace SagaChoreography.Services.Payment;

// [Payment Servisi — Domain] Ödeme saga'nın PIVOT adımıdır: başarılı olursa sipariş onaylanır.
public sealed class Payment
{
    public const decimal Limit = 10_000m;

    private Payment() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Kural (öğretim amaçlı sahte ödeme sağlayıcısı): 10.000 TRY üstü → limit aşıldı → PaymentFailed.
    public static Payment Charge(Guid orderId, Guid customerId, decimal amount)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(), OrderId = orderId, CustomerId = customerId, Amount = amount, CreatedAt = DateTime.UtcNow,
            Status = amount > Limit ? PaymentStatus.Failed : PaymentStatus.Completed
        };
        if (payment.Status == PaymentStatus.Failed)
            payment.FailureReason = $"Limit aşıldı: {amount:0.00} TRY > {Limit:0.00} TRY";
        return payment;
    }

    public bool Refund()
    {
        if (Status != PaymentStatus.Completed) return false;
        Status = PaymentStatus.Refunded;
        return true;
    }
}

public enum PaymentStatus { Completed, Failed, Refunded }
