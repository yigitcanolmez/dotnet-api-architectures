using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hexagonal.Adapters.Persistence;

public static class PersistenceAdapterExtensions
{
    public static IServiceCollection AddPersistenceAdapter(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<HexagonalDbContext>(o => o.UseSqlite(connectionString));

    // Öğretim amaçlı sadelik: migration yok, EnsureCreated + HasData seed.
    public static void EnsurePersistenceCreated(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<HexagonalDbContext>().Database.EnsureCreated();
    }
}
