using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace VerticalSlice.Tests;

// Her test sınıfı kendi SQLite dosyasıyla gerçek uygulamayı (HTTP → Endpoint → Handler → EF → DB) ayağa kaldırır.
public sealed class VerticalSliceApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbFile = $"vsa-test-{Guid.NewGuid():N}.db";

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbFile}");

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        File.Delete(_dbFile);
    }
}
