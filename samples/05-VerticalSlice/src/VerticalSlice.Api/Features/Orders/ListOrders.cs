using Microsoft.EntityFrameworkCore;
using VerticalSlice.Api.Common;
using VerticalSlice.Api.Infrastructure;

namespace VerticalSlice.Api.Features.Orders;

// [Dilim: Siparişleri Listele] Okuma dilimi: liste ekranına özel, kalem detayı olmayan hafif DTO.
// GetOrder.Response'u yeniden kullanmıyoruz; ekranlar farklı değişir (bilinçli tekrar).
public static class ListOrders
{
    public sealed record Response(Guid Id, Guid CustomerId, string Status, decimal Total, int ItemCount, DateTime CreatedAt);

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public async Task<List<Response>> HandleAsync(Guid? customerId, CancellationToken ct)
        {
            var query = db.Orders.AsNoTracking();
            if (customerId is { } cid)
                query = query.Where(o => o.CustomerId == cid);

            return await query
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new Response(o.Id, o.CustomerId, o.Status.ToString(), o.Total,
                    o.Lines.Sum(l => l.Quantity), o.CreatedAt))
                .ToListAsync(ct);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/orders", async (Guid? customerId, Handler handler, CancellationToken ct) =>
                    Results.Ok(await handler.HandleAsync(customerId, ct)))
                .WithTags("Orders")
                .WithName(nameof(ListOrders));
    }
}
