using Microsoft.EntityFrameworkCore;

namespace SagaChoreography.BuildingBlocks;

public static class DbContextRegistration
{
    // Her "servis" kendi SQLite dosyasını kullanır (database-per-service). Connection string tembel okunur ki
    // integration test'ler (WebApplicationFactory) her test için ayrı dosya verebilsin.
    public static IServiceCollection AddServiceDbContext<TDbContext>(this IServiceCollection services, string name)
        where TDbContext : DbContext =>
        services.AddDbContext<TDbContext>((sp, options) =>
            options.UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString(name)
                              ?? $"Data Source={name.ToLowerInvariant()}.db"));
}
