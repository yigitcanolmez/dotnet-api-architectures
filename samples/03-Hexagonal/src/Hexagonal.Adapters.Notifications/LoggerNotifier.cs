using Hexagonal.Core.Ports.Out;
using Microsoft.Extensions.Logging;

namespace Hexagonal.Adapters.Notifications;

// [Driven Adapter: Notifications] INotifier → konsol logu. SMTP/SMS adapter'ı ile değiştirilebilir.
public class LoggerNotifier(ILogger<LoggerNotifier> logger) : INotifier
{
    public Task NotifyAsync(Guid orderId, string message, CancellationToken ct = default)
    {
        logger.LogInformation("🔔 [Sipariş {OrderId}] {Message}", orderId, message);
        return Task.CompletedTask;
    }
}
