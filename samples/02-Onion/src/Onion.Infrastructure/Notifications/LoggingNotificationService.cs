using Microsoft.Extensions.Logging;
using Onion.Application.Interfaces;

namespace Onion.Infrastructure.Notifications;

// [Infrastructure halkası] Application'daki INotificationService'in basit implementasyonu.
// Gerçek projede SmtpEmailSender/SendGrid olurdu; değişim sadece bu halkayı etkiler.
public class LoggingNotificationService(ILogger<LoggingNotificationService> logger) : INotificationService
{
    public Task NotifyOrderPlacedAsync(Guid orderId, decimal total, CancellationToken ct = default)
    {
        logger.LogInformation("📧 Bildirim: Sipariş {OrderId} alındı. Tutar: {Total} TRY", orderId, total);
        return Task.CompletedTask;
    }

    public Task NotifyOrderShippedAsync(Guid orderId, CancellationToken ct = default)
    {
        logger.LogInformation("📧 Bildirim: Sipariş {OrderId} kargolandı.", orderId);
        return Task.CompletedTask;
    }

    public Task NotifyOrderCancelledAsync(Guid orderId, CancellationToken ct = default)
    {
        logger.LogInformation("📧 Bildirim: Sipariş {OrderId} iptal edildi.", orderId);
        return Task.CompletedTask;
    }
}
