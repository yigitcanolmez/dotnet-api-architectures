using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OrderDto = ModularMonolith.Modules.Ordering.Features.GetOrder.GetOrder.OrderDto;
using ModularMonolith.Modules.Ordering.Infrastructure;

namespace ModularMonolith.Modules.Ordering.Features.ListOrders;

internal static class ListOrders
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/orders", async (OrderingDbContext db, CancellationToken ct) =>
        {
            var orders = await db.Orders.AsNoTracking().OrderByDescending(o => o.CreatedAt).ToListAsync(ct);
            return Results.Ok(orders.Select(OrderDto.From));
        });
}
