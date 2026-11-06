using Microsoft.Extensions.Logging;
using ModularMonolith.Modules.Catalog.Contracts;
using ModularMonolith.Modules.Ordering.Contracts;
using ModularMonolith.Modules.Ordering.Infrastructure;
using ModularMonolith.Shared.Infrastructure.Inbox;

namespace ModularMonolith.Modules.Ordering.Features.CancelOrderOnStockReservationFailed;

// [Ordering — ASENKRON tepki] Catalog stok ayıramadığında (Catalog.Contracts event'i) siparişi iptal eder.
// İptal, kendi OrderCancelled event'ini doğurur → Shipping kargoyu iptal eder.
internal sealed class CancelOrderOnStockReservationFailedHandler(
    OrderingDbContext db,
    ILogger<CancelOrderOnStockReservationFailedHandler> logger)
    : IdempotentIntegrationEventHandler<StockReservationFailedIntegrationEvent, OrderingDbContext>(db)
{
    protected override async Task HandleCoreAsync(StockReservationFailedIntegrationEvent @event, CancellationToken ct)
    {
        var order = await Db.Orders.FindAsync([@event.OrderId], ct);
        if (order is null)
            return;

        var reason = $"Stok ayrılamadı: {@event.Reason}";
        var result = order.Cancel(reason);
        if (!result.IsSuccess)
        {
            logger.LogWarning("Sipariş {OrderId} otomatik iptal edilemedi: {Error}", order.Id, result.Error!.Message);
            return;
        }

        Db.AddIntegrationEvent(new OrderCancelledIntegrationEvent(order.Id, reason));
    }
}
