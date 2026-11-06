using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Shared.Abstractions.Events;

namespace ModularMonolith.Shared.Infrastructure.Messaging;

// [In-memory Event Bus] Aynı process içindeki tüm IIntegrationEventHandler<T> kayıtlarını DI'dan çözer
// ve her birini KENDİ DI scope'unda çağırır (her handler kendi modülünün DbContext'iyle çalışır).
// Mikroservise geçişte bu sınıf RabbitMQ/Kafka (ör. MassTransit) tabanlı bir IEventBus ile değişir.
internal sealed class InMemoryEventBus(IServiceScopeFactory scopeFactory) : IEventBus
{
    public async Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken ct)
    {
        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(integrationEvent.GetType());
        var handleMethod = handlerType.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!;

        int handlerCount;
        await using (var probe = scopeFactory.CreateAsyncScope())
            handlerCount = probe.ServiceProvider.GetServices(handlerType).Count();

        var errors = new List<Exception>();
        for (var i = 0; i < handlerCount; i++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetServices(handlerType).ElementAt(i)!;
            try
            {
                await (Task)handleMethod.Invoke(handler, [integrationEvent, ct])!;
            }
            catch (Exception ex)
            {
                // Bir handler'ın hatası diğerlerini engellemesin; mesaj outbox'ta tekrar denenecek,
                // başarılı handler'lar Inbox sayesinde ikinci kez çalışmayacak.
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
            throw new AggregateException($"{integrationEvent.GetType().Name} için {errors.Count} handler başarısız.", errors);
    }
}
