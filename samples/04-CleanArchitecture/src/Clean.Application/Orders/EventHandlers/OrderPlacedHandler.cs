using Clean.Application.Abstractions;
using Clean.Application.Common.Events;
using Clean.Domain.Orders.Events;
using Clean.Domain.Products;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean.Application.Orders.EventHandlers;

// [Application Katmanı – Domain Event Handler] OrderPlaced → ilgili ürünlerin stoğunu düş.
// Order aggregate'i Product'ı bilmez; iki aggregate arasındaki koordinasyon event üzerinden olur.
//
// ZAMANLAMA: Bu handler, DispatchDomainEventsInterceptor tarafından SaveChanges'ten ÖNCE çağrılır.
// Burada yaptığımız stok değişiklikleri Order INSERT'i ile AYNI SaveChanges/transaction'da yazılır.
// Stok yetersizse DomainException fırlar → hiçbir şey kaydedilmez (sipariş de oluşmaz) → 409.
public sealed class OrderPlacedHandler(IApplicationDbContext db, ILogger<OrderPlacedHandler> logger)
    : INotificationHandler<DomainEventNotification<OrderPlaced>>
{
    public async Task Handle(DomainEventNotification<OrderPlaced> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        foreach (var line in domainEvent.Lines)
        {
            // FindAsync: PlaceOrder handler'ı ürünleri zaten yüklediyse change tracker'dan döner (ek sorgu yok).
            var product = await db.Products.FindAsync([line.ProductId], cancellationToken)
                          ?? throw new InvalidOperationException($"Ürün bulunamadı: {line.ProductId}");

            product.DecreaseStock(line.Quantity);
        }

        logger.LogInformation("📦 OrderPlaced: sipariş {OrderId} için {Count} ürünün stoğu düşüldü (toplam {Total}).",
            domainEvent.OrderId, domainEvent.Lines.Count, domainEvent.Total);
    }
}
