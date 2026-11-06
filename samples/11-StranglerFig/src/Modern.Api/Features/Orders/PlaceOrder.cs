using Modern.Api.Data;
using Modern.Api.Domain;

namespace Modern.Api.Features.Orders;

// [Vertical Slice] POST /api/orders — taşınmış ilk dilim. Ürün bilgisi ACL (IProductCatalog) üzerinden legacy'den gelir.
public static class PlaceOrder
{
    public sealed record Item(Guid ProductId, int Quantity);
    public sealed record Request(Guid CustomerId, List<Item>? Items);

    public static void Map(IEndpointRouteBuilder app) => app.MapPost("/api/orders", Handle);

    private static async Task<IResult> Handle(Request req, IProductCatalog catalog, ModernDbContext db, CancellationToken ct)
    {
        IReadOnlyList<Product> products;
        try
        {
            products = await catalog.GetAllAsync(ct);
        }
        catch (HttpRequestException)
        {
            return Results.Problem("Ürün kataloğu (legacy sistem) şu an erişilemez.", statusCode: 503);
        }

        var byId = products.ToDictionary(p => p.Id);
        var items = new List<(Product, int)>();
        foreach (var i in req.Items ?? [])
        {
            if (!byId.TryGetValue(i.ProductId, out var product))
                return Results.Problem($"Bilinmeyen ürün: {i.ProductId}", statusCode: 400);
            items.Add((product, i.Quantity));
        }

        var order = Order.Place(req.CustomerId, items);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/orders/{order.Id}", new { id = order.Id });
    }
}
