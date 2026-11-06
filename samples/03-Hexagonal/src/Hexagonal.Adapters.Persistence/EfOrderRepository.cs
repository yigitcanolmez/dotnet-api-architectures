using Hexagonal.Core.Domain;
using Hexagonal.Core.Ports.Out;
using Microsoft.EntityFrameworkCore;

namespace Hexagonal.Adapters.Persistence;

// [Driven Adapter] IOrderRepository outbound port'unun EF Core/SQLite implementasyonu.
public class EfOrderRepository(HexagonalDbContext db) : IOrderRepository
{
    public Task<Order?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<IReadOnlyList<Order>> ListAsync(CancellationToken ct = default) =>
        await db.Orders.OrderByDescending(o => o.CreatedAt).ToListAsync(ct);

    public async Task AddAsync(Order order, CancellationToken ct = default) =>
        await db.Orders.AddAsync(order, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
