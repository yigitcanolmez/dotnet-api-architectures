using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ModularMonolith.Modules.Catalog.Infrastructure;

namespace ModularMonolith.Modules.Catalog.Features.ListProducts;

// [Catalog — Vertical Slice] Catalog modülü içini basit VSA/CRUD ile kurgular: endpoint + sorgu tek dosyada.
// (Modüler monolitte her modül kendi iç mimarisini seçebilir; Ordering daha zengin bir domain kullanıyor.)
internal static class ListProducts
{
    public sealed record ProductResponse(Guid Id, string Name, decimal Price, int Stock);

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/products", async (CatalogDbContext db, CancellationToken ct) =>
        {
            var products = await db.Products.AsNoTracking()
                .Select(p => new ProductResponse(p.Id, p.Name, p.Price, p.Stock))
                .ToListAsync(ct);
            return Results.Ok(products.OrderBy(p => p.Name));
        });
}
