using Onion.Domain.Entities;
using Onion.Domain.Interfaces;

namespace Onion.Application.Tests.Fakes;

// El yazımı fake: arayüz Domain'de olduğu için test istediği implementasyonu verebilir. Mock kütüphanesi gerekmez.
public class FakeOrderRepository : IOrderRepository
{
    public Dictionary<Guid, Order> Store { get; } = new();
    public int SaveChangesCalls { get; private set; }

    public Task<Order?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Store.GetValueOrDefault(id));

    public Task<IReadOnlyList<Order>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Order>>(Store.Values.ToList());

    public Task AddAsync(Order order, CancellationToken ct = default)
    {
        Store[order.Id] = order;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        SaveChangesCalls++;
        return Task.CompletedTask;
    }
}
