using Microsoft.Extensions.Options;

namespace Cqrs.Api.Projections;

// [Projections] Arka planda çalışan tüketici: kanaldan event okur → tüm projektörlere dağıtır.
// HTTP isteği bu iş bitmeden döner → okuma modeli "eventually consistent".
public sealed class ProjectionWorker(
    ProjectionChannel channel,
    IServiceScopeFactory scopeFactory,
    IOptions<ProjectionOptions> options,
    ILogger<ProjectionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var domainEvent in channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                if (options.Value.DelayMs > 0)
                    await Task.Delay(options.Value.DelayMs, stoppingToken);

                using var scope = scopeFactory.CreateScope();
                foreach (var projector in scope.ServiceProvider.GetServices<IProjector>())
                    await projector.ProjectAsync(domainEvent, stoppingToken);

                logger.LogInformation("Projeksiyon güncellendi: {Event}", domainEvent.GetType().Name);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Basitlik için logla-ve-devam et. Gerçekte: retry + dead-letter + yeniden oluşturma (rebuild).
                logger.LogError(ex, "Projeksiyon hatası: {Event}", domainEvent);
            }
        }
    }
}
