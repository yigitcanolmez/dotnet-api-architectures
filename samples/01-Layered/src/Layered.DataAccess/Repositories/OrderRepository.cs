using Layered.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Layered.DataAccess.Repositories;

// [Data Access Katmanı] SOMUT sınıf — bilinçli olarak interface YOK.
// Klasik katmanlı projelerde sık görülen hâl: Business doğrudan bu sınıfı new'ler/inject eder.
// (Interface ekleseniz bile o interface DataAccess projesinde durur; bağımlılık yönü değişmez.)
public class OrderRepository(AppDbContext db)
{
    public Task<Order?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<List<Order>> ListAsync(CancellationToken ct = default) =>
        db.Orders.Include(o => o.Lines).OrderByDescending(o => o.CreatedAt).ToListAsync(ct);

    public void Add(Order order) => db.Orders.Add(order);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
