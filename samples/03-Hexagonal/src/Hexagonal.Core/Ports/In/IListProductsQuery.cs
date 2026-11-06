namespace Hexagonal.Core.Ports.In;

// [Inbound / Driving Port] GET /products için. Web adapter katalog adapter'ını doğrudan çağırmaz; port üzerinden gider.
public interface IListProductsQuery
{
    Task<IReadOnlyList<ProductView>> ListAsync(CancellationToken ct = default);
}

public record ProductView(Guid Id, string Name, decimal Price, string Currency, int Stock);
