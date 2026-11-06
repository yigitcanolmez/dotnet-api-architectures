using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModularMonolith.Shared.Abstractions.Events;
using ModularMonolith.Shared.Infrastructure.Persistence;

namespace ModularMonolith.Shared.Infrastructure.Outbox;

// [Outbox Processor / Polling Publisher] Her modül için bir tane çalışır (OutboxProcessor<CatalogDbContext> vb.).
// İşlenmemiş outbox satırlarını sırayla okur, IEventBus'a basar ve "işlendi" olarak işaretler.
// Garanti: iş verisi kaydedildiyse event ENİNDE SONUNDA yayınlanır (at-least-once).
// Bu yüzden handler'lar idempotent olmalı → bkz. Inbox/IdempotentIntegrationEventHandler.
internal sealed class OutboxProcessor<TDbContext>(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime,
    ILogger<OutboxProcessor<TDbContext>> logger) : BackgroundService
    where TDbContext : ModuleDbContext
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Tüm modüllerin veritabanları oluşturulup host tamamen ayağa kalkana kadar bekle.
        var started = new TaskCompletionSource();
        using (lifetime.ApplicationStarted.Register(() => started.TrySetResult()))
        {
            try { await started.Task.WaitAsync(stoppingToken); }
            catch (OperationCanceledException) { return; }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Outbox ({DbContext}) işlenirken hata", typeof(TDbContext).Name);
            }

            try { await Task.Delay(PollingInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        // Çoklu instance'ta: FOR UPDATE SKIP LOCKED (PostgreSQL) / READPAST (SQL Server) gerekir.
        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null && m.Attempts < MaxAttempts)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                await bus.PublishAsync(message.ToIntegrationEvent(), ct);
                message.ProcessedOnUtc = DateTime.UtcNow;
                message.Error = null;
                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.Error = ex.Message;
                await db.SaveChangesAsync(ct);
                logger.LogWarning(ex, "Outbox mesajı {Id} ({Type}) yayınlanamadı, deneme {Attempt}",
                    message.Id, message.Type, message.Attempts);
                // Modül içi sırayı korumak için batch'i burada kesiyoruz; bir sonraki turda tekrar denenir.
                break;
            }
        }
    }
}
