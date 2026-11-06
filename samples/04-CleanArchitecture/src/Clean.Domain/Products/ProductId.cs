namespace Clean.Domain.Products;

// Product aggregate'inin kimliği. Order aggregate'i Product'a SADECE bu ID ile referans verir
// (Vernon kuralı #3: aggregate'ler arası nesne referansı değil, ID referansı).
public readonly record struct ProductId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
