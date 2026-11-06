using Microsoft.EntityFrameworkCore;
using ModularMonolith.Modules.Catalog.Domain;
using ModularMonolith.Modules.Catalog.Infrastructure;
using ModularMonolith.Modules.Ordering.Contracts;
using ModularMonolith.Shared.Infrastructure.Inbox;

namespace ModularMonolith.Modules.Catalog.Features.ReleaseStock;

// Sipariş iptal edildiğinde ayrılmış stoğu geri verir. Rezervasyon yoksa (ör. stok zaten yetersizdi) no-op.
internal sealed class ReleaseStockOnOrderCancelledHandler(CatalogDbContext db)
    : IdempotentIntegrationEventHandler<OrderCancelledIntegrationEvent, CatalogDbContext>(db)
{
    protected override async Task HandleCoreAsync(OrderCancelledIntegrationEvent @event, CancellationToken ct)
    {
        var reservation = await Db.StockReservations.SingleOrDefaultAsync(r => r.OrderId == @event.OrderId, ct);
        if (reservation is null || reservation.Status == StockReservationStatus.Released)
            return;

        var productIds = reservation.Lines.Select(l => l.ProductId).ToList();
        var products = await Db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var line in reservation.Lines)
            products[line.ProductId].Release(line.Quantity);

        reservation.MarkReleased();
    }
}
