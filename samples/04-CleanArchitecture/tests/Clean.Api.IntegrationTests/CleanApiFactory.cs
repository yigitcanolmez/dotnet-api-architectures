using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Clean.Api.IntegrationTests;

// Gerçek Program.cs'i (tüm katmanlar + pipeline + interceptor) bellek içi test sunucusunda ayağa kaldırır.
// İzolasyon: her factory örneği KENDİ SQLite dosyasını kullanır; test bitince dosya silinir.
public sealed class CleanApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(AppContext.BaseDirectory, $"clean-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools(); // dosya kilidini bırak
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
