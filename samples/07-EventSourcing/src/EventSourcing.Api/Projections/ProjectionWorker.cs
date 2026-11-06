namespace EventSourcing.Api.Projections;

// [Projections] Arka plan "catch-up subscription": event store'u periyodik olarak yoklar,
// checkpoint'ten sonraki yeni event'leri projeksiyona uygular. Uygulama kapanıp açılsa da
// checkpoint sayesinde kaldığı yerden devam eder (hiçbir event kaçmaz, hiçbiri iki kez işlenmez).
public sealed class ProjectionWorker(
    AccountBalanceProjection projection, IConfiguration config, ILogger<ProjectionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(config.GetValue("Projection:PollIntervalMs", 500));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var count = await projection.CatchUpAsync(stoppingToken);
                if (count > 0) logger.LogInformation("Projeksiyon: {Count} yeni event işlendi", count);
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Projeksiyon hatası; tekrar denenecek");
                await Task.Delay(interval, stoppingToken);
            }
        }
    }
}
