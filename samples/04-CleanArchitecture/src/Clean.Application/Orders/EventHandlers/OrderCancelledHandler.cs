using Clean.Application.Abstractions;
using Clean.Application.Common.Events;
using Clean.Domain.Orders.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean.Application.Orders.EventHandlers;

// OrderCancelled → düşülen stoğu iade et (aynı transaction içinde).
public sealed class OrderCancelledHandler(IApplicationDbContext db, ILogger<OrderCancelledHandler> logger)
    : INotificationHandler<DomainEventNotification<OrderCancelled>>
{
    public async Task Handle(DomainEventNotification<OrderCancelled> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        foreach (var line in domainEvent.Lines)
        {
            var product = await db.Products.FindAsync([line.ProductId], cancellationToken);
            product?.IncreaseStock(line.Quantity);
        }

        logger.LogInformation("↩️  OrderCancelled: sipariş {OrderId} iptal edildi ({Reason}); stoklar iade edildi.",
            domainEvent.OrderId, domainEvent.Reason);
    }
}
