namespace ModularMonolith.Modules.Catalog.Domain;

// [Catalog — iç model] internal: başka hiçbir modül bu sınıfı göremez (derleyici seviyesinde koruma).
internal sealed class Product
{
    private Product() { }

    public Product(Guid id, string name, decimal price, int stock)
    {
        Id = id;
        Name = name;
        Price = price;
        Stock = stock;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = "";
    public decimal Price { get; private set; }
    public int Stock { get; private set; }

    public bool CanReserve(int quantity) => quantity > 0 && Stock >= quantity;

    public void Reserve(int quantity)
    {
        if (!CanReserve(quantity))
            throw new InvalidOperationException($"'{Name}' için yeterli stok yok.");
        Stock -= quantity;
    }

    public void Release(int quantity) => Stock += quantity;
}
