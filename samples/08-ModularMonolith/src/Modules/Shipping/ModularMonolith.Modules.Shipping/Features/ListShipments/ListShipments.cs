using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ModularMonolith.Modules.Shipping.Infrastructure;

namespace ModularMonolith.Modules.Shipping.Features.ListShipments;

internal static class ListShipments
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/shipments", async (ShippingDbContext db, CancellationToken ct) =>
        {
            var shipments = await db.Shipments.AsNoTracking().OrderByDescending(s => s.CreatedAt).ToListAsync(ct);
            return Results.Ok(shipments.Select(ShipmentDto.From));
        });
}
