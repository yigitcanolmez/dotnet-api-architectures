using Cqrs.Api.Common;
using Dapper;

namespace Cqrs.Api.Read.Queries;

// [Read / Query] Yazma tarafında bu bilgi için GROUP BY + SUM gerekirdi; burada önceden hesaplanmış.
public sealed record GetCustomerStatsQuery(Guid CustomerId);

public sealed class CustomerStatsDto
{
    public Guid CustomerId { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSpent { get; set; }
}

public sealed class GetCustomerStatsHandler(ReadDb readDb) : IQueryHandler<GetCustomerStatsQuery, CustomerStatsDto>
{
    public async Task<CustomerStatsDto> HandleAsync(GetCustomerStatsQuery query, CancellationToken ct)
    {
        using var connection = readDb.Open();
        return await connection.QuerySingleOrDefaultAsync<CustomerStatsDto>(new CommandDefinition(
                   "SELECT * FROM CustomerStats WHERE CustomerId = @CustomerId", query, cancellationToken: ct))
               ?? new CustomerStatsDto { CustomerId = query.CustomerId };
    }
}
