using Layered.DataAccess.Entities;

namespace Layered.DataAccess;

// Tüm örneklerde ortak seed ürünleri (karşılaştırılabilirlik için sabit GUID'ler).
public static class SeedData
{
    public static readonly Product[] Products =
    [
        new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Klavye", Price = 750.00m, Currency = "TRY", Stock = 10 },
        new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Mouse", Price = 350.00m, Currency = "TRY", Stock = 20 },
        new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Name = "Monitör", Price = 6500.00m, Currency = "TRY", Stock = 5 },
    ];
}
