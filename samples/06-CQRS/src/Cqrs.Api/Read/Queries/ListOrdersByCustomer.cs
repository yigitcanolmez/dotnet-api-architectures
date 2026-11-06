using Cqrs.Api.Common;
using Dapper;

namespace Cqrs.Api.Read.Queries;

public sealed record ListOrdersByCustomerQuery(Guid CustomerId);

public sealed class ListOrdersByCustomerHandler(ReadDb readDb)
    : IQueryHandler<ListOrdersByCustomerQuery, IReadOnlyList<OrderSummaryDto>>
{
    public async Task<IReadOnlyList<OrderSummaryDto>> HandleAsync(ListOrdersByCustomerQuery query, CancellationToken ct)
    {
        using var connection = readDb.Open();
        var rows = await connection.QueryAsync<OrderSummaryDto>(new CommandDefinition(
            "SELECT * FROM OrderSummaries WHERE CustomerId = @CustomerId ORDER BY LastUpdated DESC",
            query, cancellationToken: ct));
        return rows.AsList();
    }
}
