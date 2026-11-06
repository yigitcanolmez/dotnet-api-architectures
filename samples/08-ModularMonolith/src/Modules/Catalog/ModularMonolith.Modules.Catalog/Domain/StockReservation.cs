namespace ModularMonolith.Modules.Catalog.Domain;

// Hangi sipariş için ne kadar stok ayrıldığını tutar; sipariş iptal edilince stok buradan geri verilir.
internal sealed class StockReservation
{
    private readonly List<StockReservationLine> _lines = [];

    private StockReservation() { }

    public StockReservation(Guid orderId, IEnumerable<StockReservationLine> lines)
    {
        OrderId = orderId;
        _lines.AddRange(lines);
        Status = StockReservationStatus.Reserved;
    }

    public Guid OrderId { get; private set; }
    public StockReservationStatus Status { get; private set; }
    public IReadOnlyCollection<StockReservationLine> Lines => _lines;

    public void MarkReleased() => Status = StockReservationStatus.Released;
}

internal sealed record StockReservationLine(Guid ProductId, int Quantity);

internal enum StockReservationStatus { Reserved, Released }
