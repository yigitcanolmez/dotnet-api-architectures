using Microsoft.EntityFrameworkCore;
using Onion.Domain.Entities;
using Onion.Domain.Interfaces;

namespace Onion.Infrastructure.Persistence.Repositories;

// [Infrastructure halkası] Domain'deki IOrderRepository arayüzünün EF Core implementasyonu.
// Ok yönü: Infrastructure ──► Domain (implementasyon dışarıda, sözleşme içeride).
public class EfOrderRepository(AppDbContext db) : IOrderRepository
{
    public Task<Order?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct); // owned Lines otomatik yüklenir

    public async Task<IReadOnlyList<Order>> ListAsync(CancellationToken ct = default) =>
        await db.Orders.OrderByDescending(o => o.CreatedAt).ToListAsync(ct);

    public async Task AddAsync(Order order, CancellationToken ct = default) =>
        await db.Orders.AddAsync(order, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
