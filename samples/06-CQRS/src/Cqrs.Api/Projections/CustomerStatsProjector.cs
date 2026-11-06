using Cqrs.Api.Read;
using Cqrs.Api.Write.Domain;
using Dapper;

namespace Cqrs.Api.Projections;

// [Projections] Aynı event'ten İKİNCİ bir okuma modeli: müşteri istatistikleri.
// Yeni bir ekran ihtiyacı = yeni projektör + yeni tablo; yazma tarafı hiç değişmez.
// TotalSpent/OrderCount iptal edilmemiş siparişleri sayar.
public sealed class CustomerStatsProjector(ReadDb readDb) : IProjector
{
    public async Task ProjectAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        using var connection = readDb.Open();
        switch (domainEvent)
        {
            case OrderPlaced e:
                await connection.ExecuteAsync(new CommandDefinition("""
                    INSERT INTO CustomerStats (CustomerId, OrderCount, TotalSpent) VALUES (@CustomerId, 1, @Total)
                    ON CONFLICT (CustomerId) DO UPDATE SET
                        OrderCount = OrderCount + 1,
                        TotalSpent = TotalSpent + excluded.TotalSpent
                    """, new { e.CustomerId, e.Total }, cancellationToken: ct));
                break;
            case OrderCancelled e:
                await connection.ExecuteAsync(new CommandDefinition("""
                    UPDATE CustomerStats SET OrderCount = OrderCount - 1, TotalSpent = TotalSpent - @Total
                    WHERE CustomerId = @CustomerId
                    """, new { e.CustomerId, e.Total }, cancellationToken: ct));
                break;
        }
    }
}
