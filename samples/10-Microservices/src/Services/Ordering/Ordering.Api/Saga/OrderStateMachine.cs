// [Ordering — Saga ORCHESTRATOR] İş akışının TEK ve OKUNABİLİR tanımı.
// Katılımcılar (Inventory, Payment) birbirini tanımaz; sadece komut alır, olay yayınlar.
// Akışı yöneten, hangi adımın çalışacağına ve ne zaman telafi (compensation) yapılacağına karar veren burasıdır.
//
//   Initial ──OrderSubmitted──► AwaitingStock ──StockReserved──► AwaitingPayment ──PaymentCompleted──► Completed
//                                     │                                │
//                                     └─StockReservationFailed─► Failed ◄─PaymentFailed (+ReleaseStock)─┘
using Contracts.Commands;
using Contracts.Events;
using MassTransit;

namespace Ordering.Api.Saga;

public class OrderStateMachine : MassTransitStateMachine<OrderState>
{
    public State AwaitingStock { get; private set; } = null!;
    public State AwaitingPayment { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<OrderSubmitted> OrderSubmitted { get; private set; } = null!;
    public Event<StockReserved> StockReserved { get; private set; } = null!;
    public Event<StockReservationFailed> StockReservationFailed { get; private set; } = null!;
    public Event<PaymentCompleted> PaymentCompleted { get; private set; } = null!;
    public Event<PaymentFailed> PaymentFailed { get; private set; } = null!;

    public OrderStateMachine()
    {
        InstanceState(x => x.CurrentState);

        // Correlation ID: tüm mesajlarda OrderId taşınır → doğru saga satırı bulunur.
        Event(() => OrderSubmitted, e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => StockReserved, e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => StockReservationFailed, e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentCompleted, e => e.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentFailed, e => e.CorrelateById(m => m.Message.OrderId));

        Initially(
            When(OrderSubmitted)
                .Then(c =>
                {
                    c.Saga.CustomerId = c.Message.CustomerId;
                    c.Saga.Total = c.Message.Total;
                    c.Saga.SubmittedAt = c.Message.SubmittedAt;
                    c.Saga.UpdatedAt = DateTime.UtcNow;
                })
                // T2: compensatable adım → stok ayır (COMMAND, tek alıcı)
                .Send(QueueNames.For(QueueNames.ReserveStock), c => new ReserveStock(
                    c.Message.OrderId,
                    c.Message.Items.Select(i => new StockItem(i.ProductId, i.Quantity)).ToList()))
                .TransitionTo(AwaitingStock));

        During(AwaitingStock,
            When(StockReserved)
                .Then(c => c.Saga.UpdatedAt = DateTime.UtcNow)
                // T3: pivot adım → ödeme al
                .Send(QueueNames.For(QueueNames.ProcessPayment), c => new ProcessPayment(
                    c.Saga.CorrelationId, c.Saga.CustomerId, c.Saga.Total))
                .TransitionTo(AwaitingPayment),
            When(StockReservationFailed)
                .Then(c => { c.Saga.FailureReason = c.Message.Reason; c.Saga.UpdatedAt = DateTime.UtcNow; })
                // Henüz hiçbir şey ayrılmadı → telafi edilecek adım yok, sadece reddet.
                .Publish(c => new OrderRejected(c.Saga.CorrelationId, c.Message.Reason))
                .TransitionTo(Failed));

        During(AwaitingPayment,
            When(PaymentCompleted)
                .Then(c => { c.Saga.PaymentId = c.Message.PaymentId; c.Saga.UpdatedAt = DateTime.UtcNow; })
                .Publish(c => new OrderConfirmed(c.Saga.CorrelationId))
                .TransitionTo(Completed),
            When(PaymentFailed)
                .Then(c => { c.Saga.FailureReason = c.Message.Reason; c.Saga.UpdatedAt = DateTime.UtcNow; })
                // C2: COMPENSATION — T2'nin (stok ayırma) semantik tersi. Rollback değil, yeni bir işlem!
                .Send(QueueNames.For(QueueNames.ReleaseStock), c => new ReleaseStock(
                    c.Saga.CorrelationId, c.Message.Reason))
                .Publish(c => new OrderRejected(c.Saga.CorrelationId, c.Message.Reason))
                .TransitionTo(Failed));

        // At-least-once teslimat: aynı/geciken mesajlar gelebilir → hata fırlatmak yerine yok say.
        During(AwaitingStock, Ignore(OrderSubmitted));
        During(AwaitingPayment, Ignore(OrderSubmitted), Ignore(StockReserved), Ignore(StockReservationFailed));
        During(Completed, Failed,
            Ignore(OrderSubmitted), Ignore(StockReserved), Ignore(StockReservationFailed),
            Ignore(PaymentCompleted), Ignore(PaymentFailed));

        // Completed/Failed "final" yapılmadı: saga satırı silinmesin ki GET /orders/{id}/saga ile incelenebilsin.
    }
}
