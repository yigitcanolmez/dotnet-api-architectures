using Onion.Domain.Entities;
using Onion.Domain.Exceptions;
using Onion.Domain.Interfaces;

namespace Onion.Domain.Services;

// [Domain Services halkası] Birden fazla aggregate'i (Product + Order) içeren kural:
// "Siparişteki her ürün katalogda olmalı" (Kural 5). Tek bir entity'ye ait olmadığı için domain service'te.
// Sadece Domain'deki arayüzü (IProductRepository) bilir; EF'i bilmez.
public class OrderPlacementService(IProductRepository products)
{
    public async Task<Order> PlaceAsync(Guid customerId, IReadOnlyCollection<(Guid ProductId, int Quantity)> items, DateTime now, CancellationToken ct = default)
    {
        var found = await products.GetByIdsAsync(items.Select(i => i.ProductId), ct);

        var unknown = items.Select(i => i.ProductId).Except(found.Select(p => p.Id)).ToList();
        if (unknown.Count > 0)
            throw new DomainException($"Bilinmeyen ürün: {string.Join(", ", unknown)}");

        var resolved = items.Select(i => (found.First(p => p.Id == i.ProductId), i.Quantity)).ToList();
        return Order.Place(customerId, resolved, now);
    }
}
