namespace ModularMonolith.Shared.Abstractions.Events;

// Asenkron modüller arası iletişimin soyutlaması. Bugün in-memory (aynı process);
// modül mikroservise çıkarılırken RabbitMQ/Kafka implementasyonu ile değiştirilir.
public interface IEventBus
{
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken ct);
}
