using System.Threading.Channels;

namespace Hexagonal.Adapters.Messaging;

// Gerçek bir broker (RabbitMQ, Azure Service Bus) yerine System.Threading.Channels ile process içi kuyruk.
// Öğretim amaçlı: kuyruk teknolojisi değişse bile Core etkilenmez — sadece bu adapter değişir.
public class InMemoryOrderQueue
{
    private readonly Channel<PlaceOrderMessage> _channel = Channel.CreateUnbounded<PlaceOrderMessage>();

    public ValueTask PublishAsync(PlaceOrderMessage message, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(message, ct);

    public IAsyncEnumerable<PlaceOrderMessage> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
