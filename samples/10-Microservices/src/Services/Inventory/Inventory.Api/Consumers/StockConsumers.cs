// [Inventory — Saga katılımcısı] Komut alır, kendi yerel transaction'ını yapar, sonucu EVENT olarak yayınlar.
// Saga'yı, Payment'ı veya Ordering'in iç yapısını BİLMEZ.
// Idempotency iki katmanlı:
//   1) MassTransit EF Inbox (UseEntityFrameworkOutbox) → aynı MessageId ikinci kez consumer'a ulaşmaz.
//   2) Doğal idempotency: Reservations.OrderId UNIQUE; aynı sipariş için (farklı MessageId'li) tekrar komut
//      gelirse yeni rezervasyon açılmaz, önceki sonuç tekrar yayınlanır.
using Contracts.Commands;
using Contracts.Events;
using Inventory.Api.Domain;
using Inventory.Api.Infrastructure;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Consumers;

public class ReserveStockConsumer(InventoryDbContext db, ILogger<ReserveStockConsumer> logger) : IConsumer<ReserveStock>
{
    public async Task Consume(ConsumeContext<ReserveStock> context)
    {
        var msg = context.Message;
        var ct = context.CancellationToken;

        var existing = await db.Reservations.AsNoTracking().FirstOrDefaultAsync(r => r.OrderId == msg.OrderId, ct);
        if (existing is not null)
        {
            logger.LogInformation("Tekrarlanan ReserveStock {OrderId}, önceki sonuç ({Status}) yeniden yayınlanıyor", msg.OrderId, existing.Status);
            if (existing.Status == ReservationStatus.Rejected)
                await context.Publish(new StockReservationFailed(msg.OrderId, existing.Reason ?? "Stok yetersiz"), ct);
            else if (existing.Status == ReservationStatus.Reserved)
                await context.Publish(new StockReserved(msg.OrderId), ct);
            return;
        }

        var items = msg.Items.GroupBy(i => i.ProductId)
            .Select(g => new ReservationItem { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToList();
        var ids = items.Select(i => i.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var problems = items
            .Where(i => !products.TryGetValue(i.ProductId, out var p) || !p.CanReserve(i.Quantity))
            .Select(i => products.TryGetValue(i.ProductId, out var p)
                ? $"{p.Name}: istenen {i.Quantity}, mevcut {p.AvailableStock}"
                : $"Bilinmeyen ürün {i.ProductId}")
            .ToList();

        if (problems.Count > 0)
        {
            var reason = "Stok yetersiz: " + string.Join("; ", problems);
            db.Reservations.Add(Reservation.Rejected(msg.OrderId, items, reason));
            // EF outbox açık: bu Publish OutboxMessage tablosuna yazılır, SaveChanges ile birlikte commit olur.
            await context.Publish(new StockReservationFailed(msg.OrderId, reason), ct);
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Stok rezervasyonu başarısız {OrderId}: {Reason}", msg.OrderId, reason);
            return;
        }

        foreach (var item in items) products[item.ProductId].Reserve(item.Quantity);
        db.Reservations.Add(Reservation.Reserved(msg.OrderId, items));
        await context.Publish(new StockReserved(msg.OrderId), ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Stok ayrıldı {OrderId} Kalem={ItemCount}", msg.OrderId, items.Count);
    }
}

/// <summary>Saga'nın COMPENSATION komutu.</summary>
public class ReleaseStockConsumer(StockReleaser releaser) : IConsumer<ReleaseStock>
{
    public Task Consume(ConsumeContext<ReleaseStock> context) =>
        releaser.ReleaseAsync(context.Message.OrderId, context.Message.Reason, context.CancellationToken);
}

/// <summary>Ordering'in yayınladığı olay: onaylanmış sipariş iptal edildi → stok iade.</summary>
public class OrderCancelledConsumer(StockReleaser releaser) : IConsumer<OrderCancelled>
{
    public Task Consume(ConsumeContext<OrderCancelled> context) =>
        releaser.ReleaseAsync(context.Message.OrderId, context.Message.Reason, context.CancellationToken);
}
