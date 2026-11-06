using Inventory.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await db.Database.EnsureCreatedAsync(); // Öğretim amaçlı: migration yok.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");

        if (!await db.Products.AnyAsync())
        {
            db.Products.AddRange(
                new Product(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Klavye", 750.00m, 10),
                new Product(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Mouse", 350.00m, 20),
                new Product(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Monitör", 6500.00m, 5));
            await db.SaveChangesAsync();
        }
    }
}
