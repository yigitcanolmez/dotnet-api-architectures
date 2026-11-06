using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModularMonolith.Modules.Catalog.Contracts;
using ModularMonolith.Modules.Ordering.Contracts;
using ModularMonolith.Modules.Ordering.Domain;
using ModularMonolith.Modules.Ordering.Infrastructure;
using ModularMonolith.Shared.Abstractions.Results;
using ModularMonolith.Shared.Infrastructure.Http;

namespace ModularMonolith.Modules.Ordering.Features.PlaceOrder;

// [Ordering — PlaceOrder slice] Modüller arası iletişimin İKİ biçimini de gösterir:
//   1) SENKRON: ICatalogModuleApi ile ürün adı/fiyat/stok sorgusu (Catalog.Contracts üzerinden, in-process).
//   2) ASENKRON: OrderPlacedIntegrationEvent, sipariş ile AYNI transaction'da outbox'a yazılır.
internal static class PlaceOrder
{
    public sealed record Request(Guid CustomerId, List<Item>? Items);
    public sealed record Item(Guid ProductId, int Quantity);
    public sealed record Response(Guid Id);

    internal sealed class Handler(OrderingDbContext db, ICatalogModuleApi catalog)
    {
        public async Task<Result<Guid>> HandleAsync(Request request, CancellationToken ct)
        {
            var items = request.Items ?? [];
            var productIds = items.Select(i => i.ProductId).Distinct().ToList();

            // 1) Senkron modüller arası çağrı — Catalog'un iç sınıflarını değil, sözleşmesini kullanır.
            var products = (await catalog.GetProductsAsync(productIds, ct)).ToDictionary(p => p.Id);

            var unknown = productIds.Where(id => !products.ContainsKey(id)).ToList();
            if (unknown.Count > 0)
                return Error.Validation("Order.UnknownProduct", $"Bilinmeyen ürün: {string.Join(", ", unknown)}");

            var lines = items
                .Select(i => new OrderLine(i.ProductId, products[i.ProductId].Name, i.Quantity, products[i.ProductId].Price))
                .ToList();

            var placed = Order.Place(request.CustomerId, lines, DateTime.UtcNow);
            if (!placed.IsSuccess)
                return placed.Error!;

            // Hızlı ön kontrol (okuma). Asıl stok düşümü Catalog'da ASENKRON olur; arada stok tükenirse
            // Catalog StockReservationFailed yayınlar ve sipariş otomatik iptal edilir.
            var insufficient = items.GroupBy(i => i.ProductId)
                .FirstOrDefault(g => g.Sum(i => i.Quantity) > products[g.Key].AvailableStock);
            if (insufficient is not null)
                return Error.Conflict("Order.InsufficientStock", $"'{products[insufficient.Key].Name}' için yeterli stok yok.");

            var order = placed.Value;
            db.Orders.Add(order);

            // 2) Asenkron: event'i doğrudan bus'a DEĞİL, outbox'a yaz.
            db.AddIntegrationEvent(new OrderPlacedIntegrationEvent(
                order.Id,
                order.CustomerId,
                order.Lines.Select(l => new OrderPlacedLine(l.ProductId, l.ProductName, l.Quantity)).ToList(),
                order.Total));

            await db.SaveChangesAsync(ct); // Orders + OutboxMessages → tek yerel transaction
            return order.Id;
        }
    }

    public static void Map(IEndpointRouteBuilder app) =>
        app.MapPost("/orders", async (Request request, Handler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(request, ct);
            return result.IsSuccess
                ? Results.Created($"/orders/{result.Value}", new Response(result.Value))
                : result.Error!.ToProblem();
        });
}
