using Microsoft.EntityFrameworkCore;
using Ordering.Api.Domain;

namespace Ordering.Api.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        await db.Database.EnsureCreatedAsync(); // Öğretim amaçlı: migration yok.
        // WAL: okuyucular yazıcıyı bloklamaz (HTTP sorguları + saga + outbox aynı dosyayı kullanıyor).
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");

        if (!await db.CatalogItems.AnyAsync())
        {
            db.CatalogItems.AddRange(
                new CatalogItem { ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Klavye", Price = 750.00m },
                new CatalogItem { ProductId = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Mouse", Price = 350.00m },
                new CatalogItem { ProductId = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Monitör", Price = 6500.00m });
            await db.SaveChangesAsync();
        }
    }
}
