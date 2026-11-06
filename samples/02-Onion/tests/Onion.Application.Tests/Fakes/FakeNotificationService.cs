using Onion.Application.Interfaces;

namespace Onion.Application.Tests.Fakes;

// Gönderilen bildirimleri kaydeden "spy".
public class FakeNotificationService : INotificationService
{
    public List<string> Sent { get; } = new();

    public Task NotifyOrderPlacedAsync(Guid orderId, decimal total, CancellationToken ct = default)
    {
        Sent.Add($"placed:{orderId}");
        return Task.CompletedTask;
    }

    public Task NotifyOrderShippedAsync(Guid orderId, CancellationToken ct = default)
    {
        Sent.Add($"shipped:{orderId}");
        return Task.CompletedTask;
    }

    public Task NotifyOrderCancelledAsync(Guid orderId, CancellationToken ct = default)
    {
        Sent.Add($"cancelled:{orderId}");
        return Task.CompletedTask;
    }
}
