using Clean.Application.Abstractions;
using Clean.Infrastructure.Persistence;
using Clean.Infrastructure.Persistence.Interceptors;
using Clean.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Clean.Infrastructure;

// [Infrastructure Katmanı] Application'daki portları (IApplicationDbContext, IDateTimeProvider)
// somut adaptörlere bağlar. Api sadece AddInfrastructure(config) çağırır, EF Core detayını görmez.
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<DispatchDomainEventsInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            // Bağlantı cümlesi DbContext oluşturulurken (lazy) okunur → integration testler konfigürasyonu ezebilir.
            var connectionString = configuration.GetConnectionString("Default") ?? "Data Source=clean-architecture.db";
            options.UseSqlite(connectionString);
            options.AddInterceptors(sp.GetRequiredService<DispatchDomainEventsInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        return services;
    }
}
