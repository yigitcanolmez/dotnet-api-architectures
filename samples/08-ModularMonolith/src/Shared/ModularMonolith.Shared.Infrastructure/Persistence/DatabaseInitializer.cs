using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ModularMonolith.Shared.Infrastructure.Persistence;

// Uygulama açılırken modülün veritabanını oluşturur (EnsureCreated, migration yok — öğretim amaçlı sadelik)
// ve varsa seed verisini yazar. IHostedService.StartAsync sunucu dinlemeye başlamadan önce tamamlanır.
internal sealed class DatabaseInitializer<TDbContext>(
    IServiceScopeFactory scopeFactory,
    Func<TDbContext, CancellationToken, Task>? seed) : IHostedService
    where TDbContext : ModuleDbContext
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);
        if (seed is not null)
            await seed(db, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
