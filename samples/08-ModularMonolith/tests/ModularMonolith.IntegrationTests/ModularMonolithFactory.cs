using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace ModularMonolith.IntegrationTests;

// Tüm modülleri gerçek host ile ayağa kaldırır; her fabrika kendi (izole) SQLite dosyalarını kullanır.
public sealed class ModularMonolithFactory : WebApplicationFactory<Program>
{
    private readonly string _dbDirectory =
        Path.Combine(AppContext.BaseDirectory, "test-dbs", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_dbDirectory);
        foreach (var module in new[] { "Catalog", "Ordering", "Shipping" })
            builder.UseSetting($"ConnectionStrings:{module}",
                $"Data Source={Path.Combine(_dbDirectory, module.ToLowerInvariant() + ".db")}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dbDirectory, recursive: true); } catch (IOException) { }
    }
}
