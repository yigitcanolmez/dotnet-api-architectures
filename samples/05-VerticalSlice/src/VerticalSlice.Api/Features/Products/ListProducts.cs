using Microsoft.EntityFrameworkCore;
using VerticalSlice.Api.Common;
using VerticalSlice.Api.Infrastructure;

namespace VerticalSlice.Api.Features.Products;

// [Dilim: Ürünleri Listele] En basit dilim: tek sorgu, domain yok, soyutlama yok.
public static class ListProducts
{
    public sealed record Response(Guid Id, string Name, decimal Price, string Currency, int Stock);

    public sealed class Handler(AppDbContext db) : IHandler
    {
        public Task<List<Response>> HandleAsync(CancellationToken ct) =>
            db.Products.AsNoTracking()
                .OrderBy(p => p.Id)
                .Select(p => new Response(p.Id, p.Name, p.Price, p.Currency, p.Stock))
                .ToListAsync(ct);
    }

    public sealed class Endpoint : IEndpoint
    {
        public void MapEndpoint(IEndpointRouteBuilder app) =>
            app.MapGet("/products", async (Handler handler, CancellationToken ct) =>
                    Results.Ok(await handler.HandleAsync(ct)))
                .WithTags("Products")
                .WithName(nameof(ListProducts));
    }
}
