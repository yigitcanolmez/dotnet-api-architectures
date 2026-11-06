namespace Hexagonal.Core.Ports.Out;

// [Outbound / Driven Port] Bildirim kanalı. Driven adapter: Notifications/LoggerNotifier.
public interface INotifier
{
    Task NotifyAsync(Guid orderId, string message, CancellationToken ct = default);
}
