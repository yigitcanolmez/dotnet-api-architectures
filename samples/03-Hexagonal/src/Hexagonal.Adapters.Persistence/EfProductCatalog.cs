using Hexagonal.Core.Ports.Out;
using Microsoft.EntityFrameworkCore;

namespace Hexagonal.Adapters.Persistence;

// [Driven Adapter] IProductCatalog port'u → Products tablosu. ProductRecord → CatalogProduct eşlemesi burada.
public class EfProductCatalog(HexagonalDbContext db) : IProductCatalog
{
    public async Task<IReadOnlyList<CatalogProduct>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        return await db.Products.AsNoTracking()
            .Where(p => idList.Contains(p.Id))
            .Select(p => new CatalogProduct(p.Id, p.Name, p.Price, p.Currency, p.Stock))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CatalogProduct>> ListAsync(CancellationToken ct = default) =>
        await db.Products.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new CatalogProduct(p.Id, p.Name, p.Price, p.Currency, p.Stock))
            .ToListAsync(ct);
}
