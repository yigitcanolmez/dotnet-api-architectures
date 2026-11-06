using Hexagonal.Core.Domain;
using Hexagonal.Core.Ports.Out;

namespace Hexagonal.Core.Tests.TestAdapters;

// Test için DRIVEN adapter: IOrderRepository'yi bir Dictionary ile karşılar. EF yok, SQLite yok.
public class InMemoryOrderRepository : IOrderRepository
{
    public Dictionary<Guid, Order> Orders { get; } = new();

    public Task<Order?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Orders.GetValueOrDefault(id));

    public Task<IReadOnlyList<Order>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Order>>(Orders.Values.ToList());

    public Task AddAsync(Order order, CancellationToken ct = default)
    {
        Orders[order.Id] = order;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
}
