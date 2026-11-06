using Clean.Domain.Common;
using MediatR;

namespace Clean.Application.Common.Events;

// Domain event'leri (paketsiz, saf C#) MediatR'ın INotification dünyasına taşıyan adaptör.
// Böylece Domain projesi MediatR'a bağımlı olmadan event handler'lar MediatR ile çalışır.
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;

public static class DomainEventNotification
{
    public static INotification Create(IDomainEvent domainEvent)
    {
        var type = typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType());
        return (INotification)Activator.CreateInstance(type, domainEvent)!;
    }
}
