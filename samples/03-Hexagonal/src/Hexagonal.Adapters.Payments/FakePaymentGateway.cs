using Hexagonal.Core.Ports.Out;

namespace Hexagonal.Adapters.Payments;

// [Driven Adapter: Payments] IPaymentGateway'in sahte implementasyonu.
// Gerçek hayatta StripePaymentGateway(HttpClient) olurdu; Core bu değişimden etkilenmez.
// Kural: 100.000 TRY üzerindeki tutarlar reddedilir (limit aşımı simülasyonu).
public class FakePaymentGateway : IPaymentGateway
{
    public const decimal Limit = 100_000m;

    public Task<PaymentResult> ChargeAsync(Guid customerId, decimal amount, CancellationToken ct = default) =>
        Task.FromResult(amount > Limit
            ? PaymentResult.Declined($"Tutar ({amount} TRY) kart limitini ({Limit} TRY) aşıyor.")
            : PaymentResult.Ok($"fake-tx-{Guid.NewGuid():N}"[..20]));
}
