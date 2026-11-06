namespace Clean.Domain.Common;

// Infrastructure'daki interceptor, generic tipi bilmeden event'leri toplayabilsin diye non-generic arayüz.
public interface IHasDomainEvents
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

// [Domain Katmanı] Aggregate Root: tutarlılık sınırının tek giriş kapısı.
// Dış dünya aggregate'in iç entity'lerine (ör. OrderLine) doğrudan değil, sadece root üzerinden erişir.
// Root, durum değişikliklerini domain event olarak kaydeder; yayınlamak altyapının işidir.
public abstract class AggregateRoot<TId> : Entity<TId>, IHasDomainEvents
    where TId : struct
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
