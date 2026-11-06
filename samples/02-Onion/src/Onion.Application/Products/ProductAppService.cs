using Onion.Application.Dtos;
using Onion.Domain.Interfaces;

namespace Onion.Application.Products;

public class ProductAppService(IProductRepository products)
{
    public async Task<IReadOnlyList<ProductDto>> ListAsync(CancellationToken ct = default) =>
        (await products.ListAsync(ct))
            .Select(p => new ProductDto(p.Id, p.Name, p.Price, p.Currency, p.Stock))
            .ToList();
}
