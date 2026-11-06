using Layered.DataAccess;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Layered.Tests;

// ⚠️ SORUNU GÖSTEREN SINIF:
// OrderService'in sadece "kargolanan sipariş iptal edilemez" kuralını test etmek istiyoruz.
// Ama OrderService, somut OrderRepository/ProductRepository ister; onlar da AppDbContext ister.
// Interface olmadığı için sahte (fake) bir repository veremiyoruz → gerçek bir veritabanı kurmak ZORUNDAYIZ.
// Burada SQLite in-memory kullanıyoruz: bağlantı açık kaldığı sürece veritabanı yaşar.
public sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestDatabase()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using var db = CreateContext();
        db.Database.EnsureCreated(); // şema + seed ürünler
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
