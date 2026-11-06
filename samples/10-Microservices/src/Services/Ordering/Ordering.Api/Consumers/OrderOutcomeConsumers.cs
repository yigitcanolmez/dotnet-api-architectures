// [Ordering — Consumer'lar] Saga'nın yayınladığı SONUÇ event'leriyle Order tablosunu güncelle.
// Saga "süreci", Order ise "iş verisini" tutar; ikisini ayırmak saga'yı ince tutar.
// Idempotency: endpoint'te EF inbox (UseEntityFrameworkOutbox) + Order.Confirm/Reject'in kendisi idempotent.
using Contracts.Events;
using MassTransit;
using Ordering.Api.Infrastructure;

namespace Ordering.Api.Consumers;

public class OrderConfirmedConsumer(OrderingDbContext db, ILogger<OrderConfirmedConsumer> logger) : IConsumer<OrderConfirmed>
{
    public async Task Consume(ConsumeContext<OrderConfirmed> context)
    {
        var order = await db.Orders.FindAsync([context.Message.OrderId], context.CancellationToken);
        if (order is null)
        {
            logger.LogWarning("OrderConfirmed geldi ama sipariş bulunamadı {OrderId}", context.Message.OrderId);
            return;
        }
        order.Confirm();
        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("Sipariş onaylandı {OrderId}", order.Id);
    }
}

public class OrderRejectedConsumer(OrderingDbContext db, ILogger<OrderRejectedConsumer> logger) : IConsumer<OrderRejected>
{
    public async Task Consume(ConsumeContext<OrderRejected> context)
    {
        var order = await db.Orders.FindAsync([context.Message.OrderId], context.CancellationToken);
        if (order is null)
        {
            logger.LogWarning("OrderRejected geldi ama sipariş bulunamadı {OrderId}", context.Message.OrderId);
            return;
        }
        order.Reject(context.Message.Reason);
        await db.SaveChangesAsync(context.CancellationToken);
        logger.LogInformation("Sipariş reddedildi {OrderId} Sebep={Reason}", order.Id, context.Message.Reason);
    }
}
