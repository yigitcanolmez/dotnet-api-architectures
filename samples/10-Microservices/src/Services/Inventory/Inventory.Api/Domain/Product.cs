// [Inventory — Domain] Stok, SADECE Inventory'nin verisidir. Ordering stoğu doğrudan okuyamaz/yazamaz.
namespace Inventory.Api.Domain;

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
    public int AvailableStock { get; private set; }
    public int ReservedStock { get; private set; }

    // Optimistic concurrency: iki rezervasyon aynı ürünü aynı anda düşürmeye çalışırsa biri retry'a düşer.
    public int Version { get; private set; }

    private Product() { }

    public Product(Guid id, string name, decimal price, int stock)
    {
        Id = id; Name = name; Price = price; AvailableStock = stock;
    }

    public bool CanReserve(int quantity) => AvailableStock >= quantity;

    public void Reserve(int quantity)
    {
        if (!CanReserve(quantity)) throw new InvalidOperationException($"{Name} için yeterli stok yok.");
        AvailableStock -= quantity;
        ReservedStock += quantity;
        Version++;
    }

    public void Release(int quantity)
    {
        AvailableStock += quantity;
        ReservedStock -= quantity;
        Version++;
    }
}
