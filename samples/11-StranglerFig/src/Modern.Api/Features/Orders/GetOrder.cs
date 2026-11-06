using Microsoft.EntityFrameworkCore;
using Modern.Api.Data;

namespace Modern.Api.Features.Orders;

public static class GetOrder
{
    public static void Map(IEndpointRouteBuilder app) => app.MapGet("/api/orders/{id:guid}", async (Guid id, ModernDbContext db, CancellationToken ct) =>
        await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct) is { } order
            ? Results.Ok(OrderDto.From(order))
            : Results.Problem($"Sipariş bulunamadı: {id}", statusCode: 404));
}
