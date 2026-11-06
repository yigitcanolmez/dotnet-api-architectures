using Microsoft.EntityFrameworkCore;
using ModularMonolith.Modules.Catalog.Contracts;
using ModularMonolith.Modules.Catalog.Domain;
using ModularMonolith.Modules.Catalog.Infrastructure;
using ModularMonolith.Modules.Ordering.Contracts;
using ModularMonolith.Shared.Infrastructure.Inbox;

namespace ModularMonolith.Modules.Catalog.Features.ReserveStock;

// [Catalog — ASENKRON iletişim] Ordering'in OrderPlaced event'ini dinler ve stoğu düşer.
// Ordering, Catalog'un bu işi yaptığını BİLMEZ. Stok değişikliği + sonuç event'i (outbox) + Inbox kaydı
// tek SaveChanges ile (taban sınıfta) atomik olarak yazılır.
internal sealed class ReserveStockOnOrderPlacedHandler(CatalogDbContext db)
    : IdempotentIntegrationEventHandler<OrderPlacedIntegrationEvent, CatalogDbContext>(db)
{
    protected override async Task HandleCoreAsync(OrderPlacedIntegrationEvent @event, CancellationToken ct)
    {
        var requested = @event.Lines
            .GroupBy(l => l.ProductId)
            .Select(g => new StockReservationLine(g.Key, g.Sum(l => l.Quantity)))
            .ToList();

        var productIds = requested.Select(r => r.ProductId).ToList();
        var products = await Db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var shortage = requested.FirstOrDefault(r => !products.TryGetValue(r.ProductId, out var p) || !p.CanReserve(r.Quantity));
        if (shortage is not null)
        {
            var name = products.TryGetValue(shortage.ProductId, out var p) ? p.Name : shortage.ProductId.ToString();
            Db.AddIntegrationEvent(new StockReservationFailedIntegrationEvent(@event.OrderId, $"'{name}' için yeterli stok yok."));
            return;
        }

        foreach (var line in requested)
            products[line.ProductId].Reserve(line.Quantity);

        Db.StockReservations.Add(new StockReservation(@event.OrderId, requested));
        Db.AddIntegrationEvent(new StockReservedIntegrationEvent(@event.OrderId));
    }
}
