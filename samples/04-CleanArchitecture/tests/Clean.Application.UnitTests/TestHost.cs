using Clean.Application;
using Clean.Application.Abstractions;
using Clean.Infrastructure.Persistence;
using Clean.Infrastructure.Persistence.Interceptors;
using Clean.Infrastructure.Persistence.Seed;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clean.Application.UnitTests;

// Handler'ları gerçek MediatR pipeline'ı + SQLite IN-MEMORY AppDbContext ile test ederiz.
// Bağlantı açık kaldığı sürece veritabanı yaşar; her test kendi TestHost'unu (= kendi DB'sini) oluşturur.
// EF InMemory provider yerine SQLite: gerçek SQL çevirisi ve FK/ilişki davranışı test edilir.
public sealed class TestHost : IAsyncDisposable
{
    public static readonly DateTime FixedNow = new(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    private TestHost(SqliteConnection connection, ServiceProvider provider)
    {
        _connection = connection;
        _provider = provider;
    }

    public static async Task<TestHost> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped<DispatchDomainEventsInterceptor>();
        services.AddDbContext<AppDbContext>((sp, o) => o
            .UseSqlite(connection)
            .AddInterceptors(sp.GetRequiredService<DispatchDomainEventsInterceptor>()));
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<IDateTimeProvider>(new FixedClock(FixedNow));

        var provider = services.BuildServiceProvider(validateScopes: true);
        await provider.InitialiseDatabaseAsync();

        return new TestHost(connection, provider);
    }

    // Her "istek" gerçek uygulamadaki gibi kendi scope'unda (kendi DbContext'iyle) çalışır.
    public async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    public Task RunAsync(Func<IServiceProvider, Task> action) =>
        RunAsync<object?>(async sp => { await action(sp); return null; });

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private sealed class FixedClock(DateTime now) : IDateTimeProvider
    {
        public DateTime UtcNow => now;
    }
}
