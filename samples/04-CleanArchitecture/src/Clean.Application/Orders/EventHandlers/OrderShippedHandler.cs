using Clean.Application.Common.Events;
using Clean.Domain.Orders.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean.Application.Orders.EventHandlers;

// Yan etki örneği: gerçek hayatta müşteriye e-posta/SMS (bir INotificationService portu üzerinden).
public sealed class OrderShippedHandler(ILogger<OrderShippedHandler> logger)
    : INotificationHandler<DomainEventNotification<OrderShipped>>
{
    public Task Handle(DomainEventNotification<OrderShipped> notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("🚚 OrderShipped: sipariş {OrderId} kargoya verildi, takip no: {TrackingNumber}.",
            notification.DomainEvent.OrderId, notification.DomainEvent.TrackingNumber);
        return Task.CompletedTask;
    }
}
