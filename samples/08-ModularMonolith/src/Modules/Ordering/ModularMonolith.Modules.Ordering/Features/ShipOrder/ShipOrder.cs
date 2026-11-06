using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModularMonolith.Modules.Ordering.Contracts;
using ModularMonolith.Modules.Ordering.Infrastructure;
using ModularMonolith.Shared.Abstractions.Results;
using ModularMonolith.Shared.Infrastructure.Http;

namespace ModularMonolith.Modules.Ordering.Features.ShipOrder;

internal static class ShipOrder
{
    internal sealed class Handler(OrderingDbContext db)
    {
        public async Task<Result> HandleAsync(Guid orderId, CancellationToken ct)
        {
            var order = await db.Orders.FindAsync([orderId], ct);
            if (order is null)
                return Error.NotFound("Order.NotFound", $"Sipariş bulunamadı: {orderId}");

            var result = order.Ship();
            if (!result.IsSuccess)
                return result;

            db.AddIntegrationEvent(new OrderShippedIntegrationEvent(order.Id)); // Shipping modülü dinler
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/orders/{id:guid}/ship", async (Guid id, Handler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(id, ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        });
}
