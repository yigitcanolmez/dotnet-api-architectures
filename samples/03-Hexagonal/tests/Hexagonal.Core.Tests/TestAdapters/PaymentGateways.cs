using Hexagonal.Core.Ports.Out;

namespace Hexagonal.Core.Tests.TestAdapters;

// Deterministik ödeme adapter'ları: gerçek ödeme sağlayıcısı olmadan "onay" ve "red" senaryoları.
public class AlwaysApprovePaymentGateway : IPaymentGateway
{
    public List<decimal> ChargedAmounts { get; } = new();

    public Task<PaymentResult> ChargeAsync(Guid customerId, decimal amount, CancellationToken ct = default)
    {
        ChargedAmounts.Add(amount);
        return Task.FromResult(PaymentResult.Ok("test-tx-1"));
    }
}

public class AlwaysDeclinePaymentGateway : IPaymentGateway
{
    public Task<PaymentResult> ChargeAsync(Guid customerId, decimal amount, CancellationToken ct = default) =>
        Task.FromResult(PaymentResult.Declined("Yetersiz bakiye (test)"));
}
