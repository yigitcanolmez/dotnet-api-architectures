namespace ModularMonolith.Shared.Abstractions.Events;

// Bir modül başka bir modülün olayına tepki vermek istediğinde bunu implemente eder.
// Yayıncı modül, dinleyicileri TANIMAZ (gevşek bağlılık).
public interface IIntegrationEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken ct);
}
