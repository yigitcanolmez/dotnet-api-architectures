namespace Cqrs.Api.Write.Domain;

public sealed class Product
{
    private Product() { } // EF Core için

    public Product(Guid id, string name, decimal price, int stock)
    {
        Id = id;
        Name = name;
        Price = price;
        Stock = stock;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public decimal Price { get; private set; }
    public int Stock { get; private set; }
}
