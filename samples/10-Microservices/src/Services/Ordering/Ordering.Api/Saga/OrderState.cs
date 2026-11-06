// [Ordering — Saga Durumu] Orkestratörün hafızası. Her sipariş için bir satır (CorrelationId = OrderId).
// Kalıcıdır: servis yeniden başlasa bile saga kaldığı yerden devam eder.
using MassTransit;

namespace Ordering.Api.Saga;

public class OrderState : SagaStateMachineInstance, ISagaVersion
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public decimal Total { get; set; }
    public Guid? PaymentId { get; set; }
    public string? FailureReason { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Optimistic concurrency token (bkz. OrderingDbContext.SaveChangesAsync)
    public int Version { get; set; }
}
