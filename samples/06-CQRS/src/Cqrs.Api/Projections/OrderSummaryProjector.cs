using Cqrs.Api.Read;
using Cqrs.Api.Write.Domain;
using Dapper;

namespace Cqrs.Api.Projections;

// [Projections] Write event'leri → Read tablosu (OrderSummaries). Domain nesnesi yüklemez;
// sadece event'in taşıdığı veriyle denormalize satırı oluşturur/günceller.
public sealed class OrderSummaryProjector(ReadDb readDb) : IProjector
{
    public async Task ProjectAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        using var connection = readDb.Open();
        var command = domainEvent switch
        {
            OrderPlaced e => new CommandDefinition("""
                INSERT INTO OrderSummaries (OrderId, CustomerId, Status, Total, ItemCount, ProductNames, LastUpdated)
                VALUES (@OrderId, @CustomerId, 'Placed', @Total, @ItemCount, @ProductNames, @OccurredAt)
                ON CONFLICT (OrderId) DO NOTHING
                """,
                new { e.OrderId, e.CustomerId, e.Total, e.ItemCount, ProductNames = string.Join(", ", e.ProductNames), e.OccurredAt },
                cancellationToken: ct),
            OrderShipped e => new CommandDefinition(
                "UPDATE OrderSummaries SET Status = 'Shipped', LastUpdated = @OccurredAt WHERE OrderId = @OrderId",
                new { e.OrderId, e.OccurredAt }, cancellationToken: ct),
            OrderCancelled e => new CommandDefinition(
                "UPDATE OrderSummaries SET Status = 'Cancelled', LastUpdated = @OccurredAt WHERE OrderId = @OrderId",
                new { e.OrderId, e.OccurredAt }, cancellationToken: ct),
            _ => default(CommandDefinition?)
        };

        if (command is { } c)
            await connection.ExecuteAsync(c);
    }
}
