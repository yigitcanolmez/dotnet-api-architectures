using System.Threading.Channels;
using Cqrs.Api.Write;
using Cqrs.Api.Write.Domain;

namespace Cqrs.Api.Projections;

// [Projections] Yazma ile okuma tarafı arasındaki "boru". Gerçek sistemde bu bir message broker
// (RabbitMQ, Kafka) veya Outbox tablosu olurdu; burada in-process System.Threading.Channels.
public sealed class ProjectionChannel
{
    private readonly Channel<IDomainEvent> _channel =
        Channel.CreateUnbounded<IDomainEvent>(new UnboundedChannelOptions { SingleReader = true });

    public ChannelWriter<IDomainEvent> Writer => _channel.Writer;
    public ChannelReader<IDomainEvent> Reader => _channel.Reader;
}

public sealed class ChannelEventPublisher(ProjectionChannel channel) : IEventPublisher
{
    public ValueTask PublishAsync(IDomainEvent domainEvent, CancellationToken ct) =>
        channel.Writer.WriteAsync(domainEvent, ct);
}
