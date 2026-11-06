using Cqrs.Api.Write.Domain;

namespace Cqrs.Api.Write;

// [Write] Yazma tarafı okuma tarafını TANIMAZ; sadece "olaylarımı birilerine ilet" der.
// Gerçek implementasyon Projections/ChannelEventPublisher.cs'te (bağımlılığın tersine çevrilmesi).
public interface IEventPublisher
{
    ValueTask PublishAsync(IDomainEvent domainEvent, CancellationToken ct);
}
