using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Onion.Application.Interfaces;
using Onion.Domain.Interfaces;
using Onion.Infrastructure.Notifications;
using Onion.Infrastructure.Persistence;
using Onion.Infrastructure.Persistence.Repositories;

namespace Onion.Infrastructure;

// [Infrastructure halkası] "Hangi arayüz için hangi implementasyon?" kararı burada toplanır.
// Api (composition root) sadece AddInfrastructure(...) çağırır; EF tiplerini görmek zorunda kalmaz.
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));
        services.AddScoped<IOrderRepository, EfOrderRepository>();
        services.AddScoped<IProductRepository, EfProductRepository>();
        services.AddScoped<INotificationService, LoggingNotificationService>();
        return services;
    }

    // Öğretim amaçlı sadelik: migration yok, EnsureCreated + HasData seed.
    public static void EnsureDatabaseCreated(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
    }
}
