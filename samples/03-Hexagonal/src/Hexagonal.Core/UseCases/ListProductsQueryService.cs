using Hexagonal.Core.Ports.In;
using Hexagonal.Core.Ports.Out;

namespace Hexagonal.Core.UseCases;

public class ListProductsQueryService(IProductCatalog catalog) : IListProductsQuery
{
    public async Task<IReadOnlyList<ProductView>> ListAsync(CancellationToken ct = default) =>
        (await catalog.ListAsync(ct)).Select(p => new ProductView(p.Id, p.Name, p.Price, p.Currency, p.Stock)).ToList();
}
