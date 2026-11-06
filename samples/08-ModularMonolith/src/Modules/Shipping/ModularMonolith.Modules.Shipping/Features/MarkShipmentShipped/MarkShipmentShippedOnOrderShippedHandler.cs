using Microsoft.EntityFrameworkCore;
using ModularMonolith.Modules.Ordering.Contracts;
using ModularMonolith.Modules.Shipping.Infrastructure;
using ModularMonolith.Shared.Infrastructure.Inbox;

namespace ModularMonolith.Modules.Shipping.Features.MarkShipmentShipped;

internal sealed class MarkShipmentShippedOnOrderShippedHandler(ShippingDbContext db)
    : IdempotentIntegrationEventHandler<OrderShippedIntegrationEvent, ShippingDbContext>(db)
{
    protected override async Task HandleCoreAsync(OrderShippedIntegrationEvent @event, CancellationToken ct)
    {
        // Ordering'in outbox'u event'leri sırayla bastığı için kargo kaydı normalde vardır.
        // Yoksa exception → outbox mesajı daha sonra tekrar denenir.
        var shipment = await Db.Shipments.SingleOrDefaultAsync(s => s.OrderId == @event.OrderId, ct)
                       ?? throw new InvalidOperationException($"Sipariş {@event.OrderId} için kargo kaydı henüz yok.");
        shipment.MarkShipped();
    }
}
