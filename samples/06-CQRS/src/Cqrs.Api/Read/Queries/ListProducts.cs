using Cqrs.Api.Common;
using Dapper;

namespace Cqrs.Api.Read.Queries;

// [Read / Query] Her okuma için ayrı read model şart değil: ürün listesi zaten ekrana uygun
// şekilde, bu yüzden Products tablosu Dapper ile SADECE OKUNUR (Seviye 1 CQRS yaklaşımı).
public sealed record ListProductsQuery;

public sealed class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}

public sealed class ListProductsHandler(ReadDb readDb) : IQueryHandler<ListProductsQuery, IReadOnlyList<ProductDto>>
{
    public async Task<IReadOnlyList<ProductDto>> HandleAsync(ListProductsQuery query, CancellationToken ct)
    {
        using var connection = readDb.Open();
        var rows = await connection.QueryAsync<ProductDto>(new CommandDefinition(
            "SELECT Id, Name, Price, Stock FROM Products ORDER BY Id", cancellationToken: ct));
        return rows.AsList();
    }
}
