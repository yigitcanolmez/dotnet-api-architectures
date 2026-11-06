using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace SagaChoreography.Tests;

// Gerçek host'u (3 servis + MassTransit in-memory bus) ayağa kaldırır; her fabrika kendi SQLite dosyalarını kullanır.
public sealed class SagaFactory : WebApplicationFactory<Program>
{
    private readonly string _dbDirectory = Path.Combine(AppContext.BaseDirectory, "test-dbs", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_dbDirectory);
        foreach (var service in new[] { "Ordering", "Inventory", "Payment" })
            builder.UseSetting($"ConnectionStrings:{service}",
                $"Data Source={Path.Combine(_dbDirectory, service.ToLowerInvariant() + ".db")}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dbDirectory, recursive: true); } catch (IOException) { }
    }
}
