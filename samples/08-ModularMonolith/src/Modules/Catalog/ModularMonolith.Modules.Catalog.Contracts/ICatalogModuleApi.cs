namespace ModularMonolith.Modules.Catalog.Contracts;

// [Catalog.Contracts — Public API] Diğer modüllerin Catalog'a SENKRON (in-process) erişebildiği TEK kapı.
// Mikroservise geçişte bu arayüzün implementasyonu HTTP/gRPC client olur; çağıran modül değişmez.
public interface ICatalogModuleApi
{
    Task<IReadOnlyCollection<ProductSnapshot>> GetProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct);
}

// Catalog'un iç Product entity'si DIŞARI SIZMAZ; dışarıya sadece bu anlık görüntü (snapshot) verilir.
public sealed record ProductSnapshot(Guid Id, string Name, decimal Price, int AvailableStock);
