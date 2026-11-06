using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModularMonolith.Shared.Abstractions.Events;
using ModularMonolith.Shared.Infrastructure.Messaging;
using ModularMonolith.Shared.Infrastructure.Outbox;
using ModularMonolith.Shared.Infrastructure.Persistence;

namespace ModularMonolith.Shared.Infrastructure.Modules;

// Modüllerin Add{Modül}Module() metotlarının kullandığı ortak "tesisat".
public static class ModuleRegistrationExtensions
{
    // Modülün DbContext'ini (kendi SQLite dosyası), DB başlatıcısını ve OutboxProcessor'ünü kaydeder.
    public static IServiceCollection AddModuleDbContext<TDbContext>(
        this IServiceCollection services,
        string connectionStringName,
        Func<TDbContext, CancellationToken, Task>? seed = null)
        where TDbContext : ModuleDbContext
    {
        services.TryAddSingleton<IEventBus, InMemoryEventBus>();

        // Connection string tembel okunur: test'ler (WebApplicationFactory) konfigürasyonu ezebilsin.
        services.AddDbContext<TDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString(connectionStringName)
                                   ?? $"Data Source={connectionStringName.ToLowerInvariant()}.db";
            options.UseSqlite(connectionString);
        });

        services.AddHostedService(sp => new DatabaseInitializer<TDbContext>(
            sp.GetRequiredService<IServiceScopeFactory>(), seed));
        services.AddHostedService<OutboxProcessor<TDbContext>>();
        return services;
    }

    public static IServiceCollection AddIntegrationEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IIntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        services.AddScoped<IIntegrationEventHandler<TEvent>, THandler>();
        return services;
    }
}
