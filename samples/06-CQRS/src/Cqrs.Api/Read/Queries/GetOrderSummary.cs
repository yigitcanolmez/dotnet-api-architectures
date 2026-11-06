using Cqrs.Api.Common;
using Dapper;

namespace Cqrs.Api.Read.Queries;

// [Read / Query] Join yok, hesaplama yok: ekranın istediği satır hazır bekliyor.
public sealed record GetOrderSummaryQuery(Guid OrderId);

public sealed class OrderSummaryDto
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string Status { get; set; } = default!;
    public decimal Total { get; set; }
    public int ItemCount { get; set; }
    public string ProductNames { get; set; } = default!;
    public DateTime LastUpdated { get; set; }
}

public sealed class GetOrderSummaryHandler(ReadDb readDb) : IQueryHandler<GetOrderSummaryQuery, OrderSummaryDto?>
{
    public async Task<OrderSummaryDto?> HandleAsync(GetOrderSummaryQuery query, CancellationToken ct)
    {
        using var connection = readDb.Open();
        return await connection.QuerySingleOrDefaultAsync<OrderSummaryDto>(new CommandDefinition(
            "SELECT * FROM OrderSummaries WHERE OrderId = @OrderId", query, cancellationToken: ct));
    }
}
