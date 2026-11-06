using Microsoft.EntityFrameworkCore;
using Onion.Domain.Entities;
using Onion.Domain.Interfaces;

namespace Onion.Infrastructure.Persistence.Repositories;

public class EfProductRepository(AppDbContext db) : IProductRepository
{
    public async Task<IReadOnlyList<Product>> ListAsync(CancellationToken ct = default) =>
        await db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        return await db.Products.AsNoTracking().Where(p => idList.Contains(p.Id)).ToListAsync(ct);
    }
}
