using Onion.Domain.Entities;
using Onion.Domain.Interfaces;

namespace Onion.Application.Tests.Fakes;

public class FakeProductRepository(params Product[] products) : IProductRepository
{
    public Task<IReadOnlyList<Product>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Product>>(products);

    public Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Product>>(products.Where(p => ids.Contains(p.Id)).ToList());
}
