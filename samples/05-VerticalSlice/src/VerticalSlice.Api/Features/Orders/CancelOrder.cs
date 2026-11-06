using Microsoft.EntityFrameworkCore;
using VerticalSlice.Api.Common;
using VerticalSlice.Api.Infrastructure;

namespace VerticalSlice.Api.Features.Orders;

// [Dilim: İptal Et] Yazma dilimi. İptal kuralları Domain/Order.cs'te; stok iadesi Domain/Product.cs'te.
// Handler sadece orkestrasyon yapar: yükle → karar ver (domain) → kaydet.
public static class CancelOrder
{
    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<Result> HandleAsync(Guid id, CancellationToken ct)
        {
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
            if (order is null)
                return Error.NotFound($"Sipariş bulunamadı: {id}");

            var result = order.Cancel();
            if (!result.IsSuccess) return result;

            var productIds = order.Lines.Select(l => l.ProductId).Distinct().ToList();
            var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
            foreach (var line in order.Lines)
                products[line.ProductId].Restock(line.Quantity);

            await db.SaveChangesAsync(ct);
            return Result.Success();
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapPost("/orders/{id:guid}/cancel", async (Guid id, Handler handler, CancellationToken ct) =>
                {
                    var result = await handler.HandleAsync(id, ct);
                    return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblem();
                })
                .WithTags("Orders")
                .WithName(nameof(CancelOrder));
    }
}
