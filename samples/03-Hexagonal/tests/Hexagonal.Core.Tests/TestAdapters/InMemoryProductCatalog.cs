using Hexagonal.Core.Ports.Out;

namespace Hexagonal.Core.Tests.TestAdapters;

public class InMemoryProductCatalog : IProductCatalog
{
    public static readonly CatalogProduct Klavye = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Klavye", 750m, "TRY", 10);
    public static readonly CatalogProduct Monitor = new(Guid.Parse("33333333-3333-3333-3333-333333333333"), "Monitör", 6500m, "TRY", 5);

    private readonly List<CatalogProduct> _products = [Klavye, Monitor];

    public Task<IReadOnlyList<CatalogProduct>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CatalogProduct>>(_products.Where(p => ids.Contains(p.Id)).ToList());

    public Task<IReadOnlyList<CatalogProduct>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CatalogProduct>>(_products);
}
