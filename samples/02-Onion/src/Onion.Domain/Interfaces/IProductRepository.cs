using Onion.Domain.Entities;

namespace Onion.Domain.Interfaces;

// [Domain Services halkası] Ürün okuma arayüzü.
public interface IProductRepository
{
    Task<IReadOnlyList<Product>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}
