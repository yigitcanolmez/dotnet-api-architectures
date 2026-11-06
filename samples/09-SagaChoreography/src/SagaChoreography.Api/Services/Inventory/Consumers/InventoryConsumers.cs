using MassTransit;
using Microsoft.EntityFrameworkCore;
using SagaChoreography.BuildingBlocks;
using SagaChoreography.Contracts;

namespace SagaChoreography.Services.Inventory.Consumers;

// T2: OrderCreated → stok rezerve et → StockReserved | StockReservationFailed
public sealed class ReserveStockOnOrderCreatedConsumer(InventoryDbContext db, ILogger<ReserveStockOnOrderCreatedConsumer> logger)
    : IConsumer<OrderCreated>
{
    public async Task Consume(ConsumeContext<OrderCreated> context)
    {
        if (await db.IsAlreadyProcessedAsync(context, this)) return; // Inbox
        var message = context.Message;

        var requested = message.Lines.GroupBy(l => l.ProductId)
            .Select(g => new ReservationLine(g.Key, g.Sum(l => l.Quantity))).ToList();
        var ids = requested.Select(r => r.ProductId).ToList();
        var stocks = await db.Stocks.Where(s => ids.Contains(s.ProductId)).ToDictionaryAsync(s => s.ProductId);

        var shortage = requested.FirstOrDefault(r => !stocks.TryGetValue(r.ProductId, out var s) || s.Available < r.Quantity);
        if (shortage is null)
        {
            foreach (var line in requested) stocks[line.ProductId].Reserve(line.Quantity);
            db.Reservations.Add(new Reservation(message.OrderId, requested));
            // In-memory outbox: Publish, consumer BAŞARIYLA bitene kadar bekletilir (exception → hiç gönderilmez).
            await context.Publish(new StockReserved(message.OrderId, message.CustomerId, message.Total));
            logger.LogInformation("[Inventory] {OrderId} için stok rezerve edildi", message.OrderId);
        }
        else
        {
            var name = stocks.TryGetValue(shortage.ProductId, out var s) ? s.ProductName : shortage.ProductId.ToString();
            await context.Publish(new StockReservationFailed(message.OrderId, $"'{name}' için yeterli stok yok."));
            logger.LogInformation("[Inventory] {OrderId} için stok yetersiz ({Product})", message.OrderId, name);
        }

        db.MarkAsProcessed(context, this);
        await db.SaveChangesAsync(); // stok + rezervasyon + inbox → tek transaction (concurrency çakışması → retry)
    }
}

// C2 (COMPENSATION): PaymentFailed → rezervasyonu serbest bırak, stoğu iade et.
// Dikkat: Payment bu tüketicinin varlığından habersiz. Telafi mantığı Inventory'nin içinde "gizli".
public sealed class ReleaseStockOnPaymentFailedConsumer(InventoryDbContext db, ILogger<ReleaseStockOnPaymentFailedConsumer> logger)
    : IConsumer<PaymentFailed>
{
    public async Task Consume(ConsumeContext<PaymentFailed> context)
    {
        if (await db.IsAlreadyProcessedAsync(context, this)) return;
        if (await StockReleaser.ReleaseAsync(db, context.Message.OrderId))
            logger.LogInformation("[Inventory] COMPENSATION: {OrderId} için stok iade edildi ({Reason})",
                context.Message.OrderId, context.Message.Reason);
        db.MarkAsProcessed(context, this);
        await db.SaveChangesAsync();
    }
}

// Onaylı sipariş iptal edildi → stoğu iade et.
public sealed class ReleaseStockOnOrderCancelledConsumer(InventoryDbContext db) : IConsumer<OrderCancelled>
{
    public async Task Consume(ConsumeContext<OrderCancelled> context)
    {
        if (await db.IsAlreadyProcessedAsync(context, this)) return;
        await StockReleaser.ReleaseAsync(db, context.Message.OrderId);
        db.MarkAsProcessed(context, this);
        await db.SaveChangesAsync();
    }
}

internal static class StockReleaser
{
    // Doğal idempotency: rezervasyon zaten Released ise tekrar iade edilmez.
    public static async Task<bool> ReleaseAsync(InventoryDbContext db, Guid orderId)
    {
        var reservation = await db.Reservations.SingleOrDefaultAsync(r => r.OrderId == orderId);
        if (reservation is null || reservation.Status == ReservationStatus.Released) return false;

        var ids = reservation.Lines.Select(l => l.ProductId).ToList();
        var stocks = await db.Stocks.Where(s => ids.Contains(s.ProductId)).ToDictionaryAsync(s => s.ProductId);
        foreach (var line in reservation.Lines) stocks[line.ProductId].Release(line.Quantity);
        reservation.MarkReleased();
        return true;
    }
}
