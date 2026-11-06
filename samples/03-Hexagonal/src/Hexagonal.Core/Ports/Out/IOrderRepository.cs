using Hexagonal.Core.Domain;

namespace Hexagonal.Core.Ports.Out;

// [Outbound / Driven Port] Çekirdeğin İHTİYAÇ duyduğu hizmet. Driven adapter: Persistence (EF Core).
public interface IOrderRepository
{
    Task<Order?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
