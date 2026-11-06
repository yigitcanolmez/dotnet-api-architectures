using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ModularMonolith.Modules.Shipping.Infrastructure;

namespace ModularMonolith.Modules.Shipping.Features.GetShipmentByOrder;

internal static class GetShipmentByOrder
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/shipments/by-order/{orderId:guid}", async (Guid orderId, ShippingDbContext db, CancellationToken ct) =>
        {
            var shipment = await db.Shipments.AsNoTracking().SingleOrDefaultAsync(s => s.OrderId == orderId, ct);
            // Eventual consistency: sipariş yeni verildiyse kargo kaydı henüz oluşmamış olabilir.
            return shipment is null
                ? Results.Problem(title: "Shipment.NotFound", detail: $"Sipariş için kargo kaydı (henüz) yok: {orderId}", statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(ShipmentDto.From(shipment));
        });
}
