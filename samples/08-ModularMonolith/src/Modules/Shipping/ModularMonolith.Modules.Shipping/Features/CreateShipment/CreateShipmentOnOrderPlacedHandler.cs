using Microsoft.EntityFrameworkCore;
using ModularMonolith.Modules.Ordering.Contracts;
using ModularMonolith.Modules.Shipping.Contracts;
using ModularMonolith.Modules.Shipping.Domain;
using ModularMonolith.Modules.Shipping.Infrastructure;
using ModularMonolith.Shared.Infrastructure.Inbox;

namespace ModularMonolith.Modules.Shipping.Features.CreateShipment;

// [Shipping — ASENKRON iletişim] OrderPlaced → AwaitingShipment durumunda kargo kaydı.
internal sealed class CreateShipmentOnOrderPlacedHandler(ShippingDbContext db)
    : IdempotentIntegrationEventHandler<OrderPlacedIntegrationEvent, ShippingDbContext>(db)
{
    protected override async Task HandleCoreAsync(OrderPlacedIntegrationEvent @event, CancellationToken ct)
    {
        if (await Db.Shipments.AnyAsync(s => s.OrderId == @event.OrderId, ct))
            return;

        var shipment = new Shipment(@event.OrderId, @event.Lines.Sum(l => l.Quantity), DateTime.UtcNow);
        Db.Shipments.Add(shipment);
        Db.AddIntegrationEvent(new ShipmentCreatedIntegrationEvent(shipment.Id, shipment.OrderId));
    }
}
