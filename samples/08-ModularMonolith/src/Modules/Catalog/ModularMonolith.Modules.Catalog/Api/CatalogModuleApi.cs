using Microsoft.EntityFrameworkCore;
using ModularMonolith.Modules.Catalog.Contracts;
using ModularMonolith.Modules.Catalog.Infrastructure;

namespace ModularMonolith.Modules.Catalog.Api;

// [Catalog — Public API implementasyonu] internal! Dışarıdan sadece ICatalogModuleApi arayüzü görülür.
// İç Product entity'sini ProductSnapshot'a çevirerek modül sınırında "anti-corruption" sağlar.
internal sealed class CatalogModuleApi(CatalogDbContext db) : ICatalogModuleApi
{
    public async Task<IReadOnlyCollection<ProductSnapshot>> GetProductsAsync(
        IReadOnlyCollection<Guid> productIds, CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new ProductSnapshot(p.Id, p.Name, p.Price, p.Stock))
            .ToListAsync(ct);
}
