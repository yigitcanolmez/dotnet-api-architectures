using Layered.Business.Dtos;
using Layered.DataAccess.Repositories;

namespace Layered.Business.Services;

// [Business Katmanı] "Pass-through" servis örneği: Controller → Service → Repository
// arasında hiçbir iş mantığı eklemeden sadece çağrıyı iletiyor (katmanlı mimarinin tipik eksisi).
public class ProductService(ProductRepository products)
{
    public async Task<IReadOnlyList<ProductDto>> ListAsync(CancellationToken ct = default) =>
        (await products.ListAsync(ct))
            .Select(p => new ProductDto(p.Id, p.Name, p.Price, p.Currency, p.Stock))
            .ToList();
}
