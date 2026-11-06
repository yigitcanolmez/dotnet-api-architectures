using Modern.Api.Data;

namespace Modern.Api.Features.Orders;

public static class ShipOrder
{
    public static void Map(IEndpointRouteBuilder app) => app.MapPost("/api/orders/{id:guid}/ship", async (Guid id, ModernDbContext db, CancellationToken ct) =>
    {
        var order = await db.Orders.FindAsync([id], ct);
        if (order is null) return Results.Problem($"Sipariş bulunamadı: {id}", statusCode: 404);
        order.Ship();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    });
}
