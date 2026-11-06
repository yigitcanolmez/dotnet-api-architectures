namespace Cqrs.Api.Write.Domain;

// [Write / Domain] Yazma tarafında olan biteni anlatan olaylar. Okuma modelini güncellemek
// için gereken veriyi TAŞIRLAR (event-carried state) → projektörler yazma DB'sini sorgulamak zorunda kalmaz.
public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}

public sealed record OrderPlaced(
    Guid OrderId, Guid CustomerId, decimal Total, int ItemCount, IReadOnlyList<string> ProductNames, DateTime OccurredAt)
    : IDomainEvent;

public sealed record OrderShipped(Guid OrderId, Guid CustomerId, DateTime OccurredAt) : IDomainEvent;

public sealed record OrderCancelled(Guid OrderId, Guid CustomerId, decimal Total, DateTime OccurredAt) : IDomainEvent;
