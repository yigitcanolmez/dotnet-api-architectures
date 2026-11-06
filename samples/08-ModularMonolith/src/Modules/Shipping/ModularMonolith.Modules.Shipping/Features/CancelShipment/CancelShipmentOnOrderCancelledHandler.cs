using Microsoft.EntityFrameworkCore;
using ModularMonolith.Modules.Ordering.Contracts;
using ModularMonolith.Modules.Shipping.Infrastructure;
using ModularMonolith.Shared.Infrastructure.Inbox;

namespace ModularMonolith.Modules.Shipping.Features.CancelShipment;

internal sealed class CancelShipmentOnOrderCancelledHandler(ShippingDbContext db)
    : IdempotentIntegrationEventHandler<OrderCancelledIntegrationEvent, ShippingDbContext>(db)
{
    protected override async Task HandleCoreAsync(OrderCancelledIntegrationEvent @event, CancellationToken ct)
    {
        var shipment = await Db.Shipments.SingleOrDefaultAsync(s => s.OrderId == @event.OrderId, ct);
        shipment?.Cancel();
    }
}
