using Hexagonal.Core.Ports.Out;

namespace Hexagonal.Core.Tests.TestAdapters;

public class RecordingNotifier : INotifier
{
    public List<(Guid OrderId, string Message)> Notifications { get; } = new();

    public Task NotifyAsync(Guid orderId, string message, CancellationToken ct = default)
    {
        Notifications.Add((orderId, message));
        return Task.CompletedTask;
    }
}
