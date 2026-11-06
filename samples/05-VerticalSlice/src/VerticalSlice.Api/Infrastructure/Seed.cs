using Microsoft.EntityFrameworkCore;
using VerticalSlice.Api.Domain;

namespace VerticalSlice.Api.Infrastructure;

// [Infrastructure] Öğretim amaçlı sadelik: migration yerine EnsureCreated + sabit GUID'li seed.
public static class Seed
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        if (await db.Products.AnyAsync())
            return;

        db.Products.AddRange(
            new Product(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Klavye", 750.00m, 10),
            new Product(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Mouse", 350.00m, 20),
            new Product(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Monitör", 6500.00m, 5));
        await db.SaveChangesAsync();
    }
}
