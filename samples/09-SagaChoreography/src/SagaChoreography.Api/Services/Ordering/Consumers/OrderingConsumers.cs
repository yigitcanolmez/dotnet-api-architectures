using MassTransit;
using SagaChoreography.BuildingBlocks;
using SagaChoreography.Contracts;

namespace SagaChoreography.Services.Ordering.Consumers;

// [Ordering Servisi — Saga'nın SONU] Ordering, akışın sonunu diğer servislerin olaylarından öğrenir.
// Choreography'nin eksisi burada görünür: "Sipariş ne zaman onaylanır?" sorusunun cevabı bu dosyada,
// "ne zaman reddedilir?" sorusunun cevabı ise Inventory ve Payment'taki kurallara dağılmış durumda.

// T4: PaymentCompleted → Confirmed
public sealed class ConfirmOrderOnPaymentCompletedConsumer(OrderingDbContext db) : IConsumer<PaymentCompleted>
{
    public Task Consume(ConsumeContext<PaymentCompleted> context) =>
        OrderUpdater.UpdateAsync(db, context, this, context.Message.OrderId, o => o.Confirm());
}

// T2 başarısız: StockReservationFailed → Rejected (telafi edilecek önceki adım yok)
public sealed class RejectOrderOnStockReservationFailedConsumer(OrderingDbContext db) : IConsumer<StockReservationFailed>
{
    public Task Consume(ConsumeContext<StockReservationFailed> context) =>
        OrderUpdater.UpdateAsync(db, context, this, context.Message.OrderId, o => o.Reject(context.Message.Reason));
}

// C1: PaymentFailed → Rejected (Inventory aynı olayı dinleyip stoğu iade eder — C2)
public sealed class RejectOrderOnPaymentFailedConsumer(OrderingDbContext db) : IConsumer<PaymentFailed>
{
    public Task Consume(ConsumeContext<PaymentFailed> context) =>
        OrderUpdater.UpdateAsync(db, context, this, context.Message.OrderId, o => o.Reject(context.Message.Reason));
}

internal static class OrderUpdater
{
    public static async Task UpdateAsync<T>(OrderingDbContext db, ConsumeContext<T> context, object consumer, Guid orderId, Action<Order> change)
        where T : class
    {
        if (await db.IsAlreadyProcessedAsync(context, consumer)) return; // Inbox

        var order = await db.Orders.FindAsync([orderId], context.CancellationToken)
                    ?? throw new InvalidOperationException($"Sipariş {orderId} bulunamadı."); // retry
        change(order);

        db.MarkAsProcessed(context, consumer);
        await db.SaveChangesAsync(context.CancellationToken); // durum değişikliği + inbox kaydı → tek transaction
    }
}
