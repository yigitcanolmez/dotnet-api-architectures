// [Inventory — API] Sadece okuma uçları. Stok değişikliği yalnızca mesajlarla olur.
using Inventory.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Endpoints;

public record ProductDto(Guid Id, string Name, decimal Price, int AvailableStock, int ReservedStock);
public record ReservationItemDto(Guid ProductId, int Quantity);
public record ReservationDto(Guid Id, Guid OrderId, string Status, string? Reason, DateTime CreatedAt,
    DateTime? ReleasedAt, IReadOnlyList<ReservationItemDto> Items);

public static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/products", async (InventoryDbContext db, CancellationToken ct) =>
            (await db.Products.AsNoTracking().ToListAsync(ct))
                .OrderBy(p => p.Name)
                .Select(p => new ProductDto(p.Id, p.Name, p.Price, p.AvailableStock, p.ReservedStock)));

        app.MapGet("/products/{id:guid}", async (Guid id, InventoryDbContext db, CancellationToken ct) =>
            await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct) is { } p
                ? Results.Ok(new ProductDto(p.Id, p.Name, p.Price, p.AvailableStock, p.ReservedStock))
                : Results.Problem(statusCode: 404, title: "Ürün bulunamadı"));

        // ?orderId=... ile filtrelenebilir (Gateway'deki API Composition bunu kullanır).
        app.MapGet("/reservations", async (Guid? orderId, InventoryDbContext db, CancellationToken ct) =>
        {
            var query = db.Reservations.AsNoTracking();
            if (orderId is not null) query = query.Where(r => r.OrderId == orderId);
            return (await query.ToListAsync(ct))
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ReservationDto(r.Id, r.OrderId, r.Status.ToString(), r.Reason, r.CreatedAt, r.ReleasedAt,
                    r.Items.Select(i => new ReservationItemDto(i.ProductId, i.Quantity)).ToList()));
        });
    }
}
