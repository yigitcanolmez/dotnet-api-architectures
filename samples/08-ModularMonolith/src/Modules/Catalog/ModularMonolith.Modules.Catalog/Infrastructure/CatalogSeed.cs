using Microsoft.EntityFrameworkCore;
using ModularMonolith.Modules.Catalog.Domain;

namespace ModularMonolith.Modules.Catalog.Infrastructure;

internal static class CatalogSeed
{
    public static async Task SeedAsync(CatalogDbContext db, CancellationToken ct)
    {
        if (await db.Products.AnyAsync(ct))
            return;

        db.Products.AddRange(
            new Product(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Klavye", 750.00m, 10),
            new Product(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Mouse", 350.00m, 20),
            new Product(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Monitör", 6500.00m, 5));
        await db.SaveChangesAsync(ct);
    }
}
