using Microsoft.EntityFrameworkCore;
using Modern.Api.Data;

namespace Modern.Api.Features.Orders;

public static class ListOrders
{
    public static void Map(IEndpointRouteBuilder app) => app.MapGet("/api/orders", async (ModernDbContext db, CancellationToken ct) =>
    {
        var orders = await db.Orders.AsNoTracking().ToListAsync(ct);
        return orders.OrderByDescending(o => o.CreatedAt).Select(OrderDto.From);
    });
}
