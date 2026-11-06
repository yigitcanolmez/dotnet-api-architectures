using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Cqrs.Tests;

public sealed class CqrsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbFile = $"cqrs-test-{Guid.NewGuid():N}.db";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbFile}");
        builder.UseSetting("Projection:DelayMs", "300");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        File.Delete(_dbFile);
    }
}
