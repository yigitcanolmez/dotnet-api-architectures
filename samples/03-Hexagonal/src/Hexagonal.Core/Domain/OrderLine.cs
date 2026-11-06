namespace Hexagonal.Core.Domain;

public class OrderLine
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = "";
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal => UnitPrice * Quantity;

    private OrderLine() { } // persistence adapter (EF) için

    internal OrderLine(Guid productId, string productName, int quantity, decimal unitPrice)
    {
        // Kural 2: Quantity > 0
        if (quantity <= 0) throw new DomainException("Miktar 0'dan büyük olmalıdır.");
        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
