using Onion.Domain.Entities;

namespace Onion.Domain.Interfaces;

// [Domain Services halkası] Repository ARAYÜZÜ içeride tanımlanır; implementasyonu
// en dış halkada (Onion.Infrastructure/Persistence/Repositories/EfOrderRepository.cs).
// Katmanlı mimariden tek fark bu: bağımlılık tersine döndü (Dependency Inversion).
public interface IOrderRepository
{
    Task<Order?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> ListAsync(CancellationToken ct = default);
    Task AddAsync(Order order, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
