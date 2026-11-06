namespace Onion.Application.Interfaces;

// [Application Services halkası] Uygulamanın ihtiyaç duyduğu dış hizmetin ARAYÜZÜ.
// Implementasyon: Onion.Infrastructure/Notifications/LoggingNotificationService.cs
// (yarın SMTP/SendGrid olursa sadece Infrastructure değişir).
public interface INotificationService
{
    Task NotifyOrderPlacedAsync(Guid orderId, decimal total, CancellationToken ct = default);
    Task NotifyOrderShippedAsync(Guid orderId, CancellationToken ct = default);
    Task NotifyOrderCancelledAsync(Guid orderId, CancellationToken ct = default);
}
