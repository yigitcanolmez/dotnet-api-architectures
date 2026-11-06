using MassTransit;
using Microsoft.EntityFrameworkCore;
using SagaChoreography.BuildingBlocks;
using SagaChoreography.Contracts;

namespace SagaChoreography.Services.Payment.Consumers;

// T3: StockReserved → ödeme al → PaymentCompleted | PaymentFailed
// Payment, Ordering'i veya Inventory'yi tanımaz; sadece StockReserved olayını tanır.
public sealed class ChargePaymentOnStockReservedConsumer(PaymentDbContext db, ILogger<ChargePaymentOnStockReservedConsumer> logger)
    : IConsumer<StockReserved>
{
    public async Task Consume(ConsumeContext<StockReserved> context)
    {
        if (await db.IsAlreadyProcessedAsync(context, this)) return; // Inbox
        var message = context.Message;

        if (await db.Payments.AnyAsync(p => p.OrderId == message.OrderId)) return; // doğal idempotency (unique OrderId)

        var payment = Payment.Charge(message.OrderId, message.CustomerId, message.Total);
        db.Payments.Add(payment);

        if (payment.Status == PaymentStatus.Completed)
            await context.Publish(new PaymentCompleted(message.OrderId, payment.Id, payment.Amount));
        else
            await context.Publish(new PaymentFailed(message.OrderId, payment.FailureReason!)); // → compensation başlar

        logger.LogInformation("[Payment] {OrderId}: {Status} {Reason}", message.OrderId, payment.Status, payment.FailureReason ?? "");
        db.MarkAsProcessed(context, this);
        await db.SaveChangesAsync();
    }
}

// Onaylı sipariş iptal edildi → ödemeyi iade et.
public sealed class RefundPaymentOnOrderCancelledConsumer(PaymentDbContext db) : IConsumer<OrderCancelled>
{
    public async Task Consume(ConsumeContext<OrderCancelled> context)
    {
        if (await db.IsAlreadyProcessedAsync(context, this)) return;
        var payment = await db.Payments.SingleOrDefaultAsync(p => p.OrderId == context.Message.OrderId);
        payment?.Refund();
        db.MarkAsProcessed(context, this);
        await db.SaveChangesAsync();
    }
}
