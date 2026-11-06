using Clean.Domain.Orders;
using Clean.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clean.Infrastructure.Persistence.Seed;

// Öğretim amaçlı sadelik: migration yerine EnsureCreated + sabit GUID'li seed.
public static class AppDbContextSeed
{
    public static readonly ProductId KeyboardId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    public static readonly ProductId MouseId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    public static readonly ProductId MonitorId = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));

    public static async Task InitialiseDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await SeedAsync(db, cancellationToken);
    }

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Products.AnyAsync(cancellationToken)) return;

        db.Products.AddRange(
            Product.Create(KeyboardId, "Klavye", new Money(750.00m, "TRY"), 10),
            Product.Create(MouseId, "Mouse", new Money(350.00m, "TRY"), 20),
            Product.Create(MonitorId, "Monitör", new Money(6500.00m, "TRY"), 5));

        await db.SaveChangesAsync(cancellationToken);
    }
}
