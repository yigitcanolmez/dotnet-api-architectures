using Clean.Application.Common.Events;
using Clean.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Clean.Infrastructure.Persistence.Interceptors;

// [Infrastructure Katmanı] SaveChanges'e takılıp aggregate'lerin biriktirdiği domain event'leri yayınlar.
//
// SEÇİM: SaveChanges'ten ÖNCE (SavingChangesAsync) yayınlıyoruz.
//  + Handler'ların yaptığı değişiklikler (ör. stok düşme) aynı SaveChanges'te → tek transaction, atomik.
//  + Handler hata fırlatırsa (yetersiz stok) hiçbir şey yazılmaz.
//  - Handler'lar "henüz kaydedilmemiş" veriyi görür; e-posta gibi geri alınamaz yan etkiler için uygun DEĞİL.
//    Onlar için SavedChangesAsync (sonra) + Outbox deseni kullanılmalı (bkz. README).
public sealed class DispatchDomainEventsInterceptor(IPublisher publisher) : SaveChangesInterceptor
{
    private const int MaxDispatchRounds = 10;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        throw new NotSupportedException("Domain event'lerin yayınlanması için SaveChangesAsync kullanın.");

    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        // Handler'lar yeni event üretebilir (zincirleme); hiç event kalmayana dek tur atıyoruz.
        for (var round = 0; round < MaxDispatchRounds; round++)
        {
            var aggregates = context.ChangeTracker
                .Entries<IHasDomainEvents>()
                .Select(e => e.Entity)
                .Where(a => a.DomainEvents.Count != 0)
                .ToList();

            if (aggregates.Count == 0) break;

            var events = aggregates.SelectMany(a => a.DomainEvents).ToList();
            aggregates.ForEach(a => a.ClearDomainEvents());

            foreach (var domainEvent in events)
                await publisher.Publish(DomainEventNotification.Create(domainEvent), cancellationToken);
        }

        // Handler'ların değiştirdiği entity'lerin (ör. Product.Stock) kaydedilmek üzere algılandığından emin ol.
        context.ChangeTracker.DetectChanges();
    }
}
