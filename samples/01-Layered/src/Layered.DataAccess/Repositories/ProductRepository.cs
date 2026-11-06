using Layered.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Layered.DataAccess.Repositories;

// [Data Access Katmanı] Somut repository, interface yok.
public class ProductRepository(AppDbContext db)
{
    public Task<List<Product>> ListAsync(CancellationToken ct = default) =>
        db.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

    public Task<List<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.Distinct().ToList();
        return db.Products.Where(p => idList.Contains(p.Id)).ToListAsync(ct);
    }
}
