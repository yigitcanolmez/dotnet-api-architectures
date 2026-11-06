using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModularMonolith.Modules.Ordering.Contracts;
using ModularMonolith.Modules.Ordering.Infrastructure;
using ModularMonolith.Shared.Abstractions.Results;
using ModularMonolith.Shared.Infrastructure.Http;

namespace ModularMonolith.Modules.Ordering.Features.CancelOrder;

// [Ordering — CancelOrder slice] Siparişi iptal eder ve OrderCancelled event'ini outbox'a yazar.
// Ordering, iptalin sonuçlarını (stok iadesi, kargonun iptali) BİLMEZ; Catalog ve Shipping kendileri tepki verir.
internal static class CancelOrder
{
    internal sealed class Handler(OrderingDbContext db)
    {
        public async Task<Result> HandleAsync(Guid orderId, string reason, CancellationToken ct)
        {
            var order = await db.Orders.FindAsync([orderId], ct);
            if (order is null)
                return Error.NotFound("Order.NotFound", $"Sipariş bulunamadı: {orderId}");

            var result = order.Cancel(reason); // invariant: Shipped/Cancelled → 409
            if (!result.IsSuccess)
                return result;

            db.AddIntegrationEvent(new OrderCancelledIntegrationEvent(order.Id, reason));
            await db.SaveChangesAsync(ct); // Orders + OutboxMessages → tek transaction
            return Result.Success();
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/orders/{id:guid}/cancel", async (Guid id, Handler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(id, "Müşteri iptal etti", ct);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
        });
}
