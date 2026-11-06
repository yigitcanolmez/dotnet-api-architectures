using VerticalSlice.Api.Common;
using VerticalSlice.Api.Infrastructure;

namespace VerticalSlice.Api.Features.Orders;

// [Dilim: Kargola] Yazma dilimi: aggregate'i yükle → domain metodunu çağır → kaydet.
// "Sadece Placed sipariş kargolanır" kuralı Domain/Order.cs içinde.
public static class ShipOrder
{
    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result> HandleAsync(Guid id, CancellationToken ct)
        {
            var order = await db.Orders.FindAsync([id], ct);
            if (order is null)
                return Error.NotFound($"Sipariş bulunamadı: {id}");

            var result = order.Ship();
            if (!result.IsSuccess) return result;

            await db.SaveChangesAsync(ct);
            return Result.Success();
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/orders/{id:guid}/ship", async (Guid id, Handler handler, CancellationToken ct) =>
                {
                    var result = await handler.HandleAsync(id, ct);
                    return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
                })
                .WithTags("Orders")
                .WithName(nameof(ShipOrder));
    }
}
