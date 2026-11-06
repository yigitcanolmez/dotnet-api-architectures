namespace Modern.Api.Domain;

// [Domain portu] Domain kendi dilinde ister: "bana ürünleri ver".
// Ürünlerin nereden (legacy HTTP, ileride kendi DB'miz) geldiğini bilmez.
public interface IProductCatalog
{
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct);
}
