using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ModularMonolith.Modules.Ordering.Domain;
using ModularMonolith.Modules.Ordering.Infrastructure;

namespace ModularMonolith.Modules.Ordering.Features.GetOrder;

internal static class GetOrder
{
    public sealed record OrderLineDto(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice);

    public sealed record OrderDto(
        Guid Id, Guid CustomerId, string Status, decimal Total, DateTime CreatedAt,
        string? CancellationReason, IReadOnlyList<OrderLineDto> Lines)
    {
        public static OrderDto From(Order o) => new(
            o.Id, o.CustomerId, o.Status.ToString(), o.Total, o.CreatedAt, o.CancellationReason,
            o.Lines.Select(l => new OrderLineDto(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice)).ToList());
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/orders/{id:guid}", async (Guid id, OrderingDbContext db, CancellationToken ct) =>
        {
            var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id, ct);
            return order is null
                ? Results.Problem(title: "Order.NotFound", detail: $"Sipariş bulunamadı: {id}", statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(OrderDto.From(order));
        });
}
