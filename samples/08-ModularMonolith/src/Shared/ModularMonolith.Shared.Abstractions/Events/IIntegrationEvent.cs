namespace ModularMonolith.Shared.Abstractions.Events;

// [Shared.Abstractions] Modüller arası (bounded context'ler arası) PUBLIC sözleşmenin işaretçisi.
// Integration event = "geçmişte olmuş bir olgu"; minimal tutulur ve versiyonlanır.
// Id, Inbox (idempotency) tarafında "bu mesajı zaten işledim mi?" sorusunun anahtarıdır.
public interface IIntegrationEvent
{
    Guid Id { get; }
    DateTime OccurredOnUtc { get; }
}

public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
