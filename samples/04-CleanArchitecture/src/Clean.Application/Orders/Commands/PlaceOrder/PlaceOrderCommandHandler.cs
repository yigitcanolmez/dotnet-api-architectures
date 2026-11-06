using Clean.Application.Abstractions;
using Clean.Application.Common.Exceptions;
using Clean.Domain.Orders;
using Clean.Domain.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Clean.Application.Orders.Commands.PlaceOrder;

// [Application Katmanı – Use Case / Interactor] Orkestrasyon yapar, iş kuralı İÇERMEZ:
// 1) ürünleri yükle, 2) Order.Place ile aggregate'i oluştur (kurallar domain'de), 3) kaydet.
// Stok düşme burada YAPILMAZ: Order.Place, OrderPlaced event'i üretir; OrderPlacedHandler stoğu düşer.
public sealed class PlaceOrderCommandHandler(IApplicationDbContext db, IDateTimeProvider clock)
    : IRequestHandler<PlaceOrderCommand, Guid>
{
    public async Task<Guid> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var productIds = request.Items.Select(i => new ProductId(i.ProductId)).ToList();

        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var items = request.Items.Select(i =>
        {
            var productId = new ProductId(i.ProductId);
            if (!products.TryGetValue(productId, out var product))
                throw new NotFoundException(nameof(Product), i.ProductId);

            // Snapshot: ad ve fiyat sipariş anındaki haliyle kopyalanır.
            return new OrderItem(productId, product.Name, product.Price, i.Quantity);
        }).ToList();

        var order = Order.Place(new CustomerId(request.CustomerId), items, clock.UtcNow);

        db.Orders.Add(order);
        await db.SaveChangesAsync(cancellationToken); // → DispatchDomainEventsInterceptor → OrderPlacedHandler

        return order.Id.Value;
    }
}
