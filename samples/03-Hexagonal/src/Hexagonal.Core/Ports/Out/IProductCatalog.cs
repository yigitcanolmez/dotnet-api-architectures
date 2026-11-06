namespace Hexagonal.Core.Ports.Out;

// [Outbound / Driven Port] Ürün kataloğu "dış dünya"dır: bugün SQLite tablosu, yarın bir HTTP servisi olabilir.
public interface IProductCatalog
{
    Task<IReadOnlyList<CatalogProduct>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<IReadOnlyList<CatalogProduct>> ListAsync(CancellationToken ct = default);
}

public record CatalogProduct(Guid Id, string Name, decimal Price, string Currency, int Stock);
