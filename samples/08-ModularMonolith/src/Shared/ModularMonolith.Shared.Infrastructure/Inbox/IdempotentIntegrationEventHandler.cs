using Microsoft.EntityFrameworkCore;
using ModularMonolith.Shared.Abstractions.Events;
using ModularMonolith.Shared.Infrastructure.Persistence;

namespace ModularMonolith.Shared.Infrastructure.Inbox;

// [Inbox / Idempotent Consumer] Outbox at-least-once teslim eder; aynı event iki kez gelebilir.
// Bu taban sınıf: (1) daha önce işlendiyse atlar, (2) iş değişikliklerini ve Inbox kaydını
// TEK SaveChanges (= tek yerel transaction) ile kalıcı hale getirir → effectively-once processing.
// Türeyen sınıflar HandleCoreAsync içinde SaveChanges ÇAĞIRMAMALI.
public abstract class IdempotentIntegrationEventHandler<TEvent, TDbContext>(TDbContext db)
    : IIntegrationEventHandler<TEvent>
    where TEvent : IIntegrationEvent
    where TDbContext : ModuleDbContext
{
    protected TDbContext Db { get; } = db;

    public async Task HandleAsync(TEvent integrationEvent, CancellationToken ct)
    {
        var handlerName = GetType().FullName!;

        var alreadyProcessed = await Db.InboxMessages
            .AnyAsync(x => x.EventId == integrationEvent.Id && x.HandlerName == handlerName, ct);
        if (alreadyProcessed)
            return;

        await HandleCoreAsync(integrationEvent, ct);

        Db.InboxMessages.Add(new InboxMessage(integrationEvent.Id, handlerName));
        await Db.SaveChangesAsync(ct);
    }

    protected abstract Task HandleCoreAsync(TEvent integrationEvent, CancellationToken ct);
}
