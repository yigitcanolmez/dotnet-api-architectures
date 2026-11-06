using Clean.Domain.Common;
using Clean.Domain.Orders.Events;

namespace Clean.Domain.Orders;

// [Domain Katmanı – Enterprise Business Rules] Order aggregate root'u.
// - Public setter yok: durum sadece niyet bildiren metotlarla (Place/Ship/Cancel) değişir (Ubiquitous Language).
// - Tüm invariant'lar burada korunur; Application katmanı kuralları "bilmez", sadece çağırır.
// - Her önemli değişiklik bir domain event üretir.
public sealed class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderLine> _lines = [];

    public CustomerId CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public Money Total { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }
    public string? TrackingNumber { get; private set; }
    public string? CancellationReason { get; private set; }

    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    private Order() { } // EF Core için

    // Factory: geçerli olmayan bir Order nesnesinin hiç var olamamasını garanti eder.
    public static Order Place(CustomerId customerId, IEnumerable<OrderItem> items, DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(items);
        var itemList = items.ToList();

        if (itemList.Count == 0) throw new DomainException(OrderErrors.NoLines);
        if (itemList.Count > OrderErrors.MaxLines) throw new DomainException(OrderErrors.TooManyLines);

        var order = new Order
        {
            Id = OrderId.New(),
            CustomerId = customerId,
            Status = OrderStatus.Placed,
            CreatedAtUtc = createdAtUtc,
            Total = Money.Zero(itemList[0].UnitPrice.Currency)
        };

        foreach (var item in itemList)
        {
            var line = new OrderLine(item.ProductId, item.ProductName, item.UnitPrice, item.Quantity);
            order._lines.Add(line);
            order.Total += line.SubTotal; // farklı para birimi → Money DomainException fırlatır
        }

        order.Raise(new OrderPlaced(order.Id, customerId, order.Total, order.SnapshotLines()));
        return order;
    }

    public Result CanShip() =>
        Status == OrderStatus.Placed ? Result.Success() : Result.Failure(OrderErrors.OnlyPlacedCanBeShipped);

    public void Ship(string trackingNo)
    {
        if (string.IsNullOrWhiteSpace(trackingNo)) throw new DomainException(OrderErrors.TrackingNumberRequired);
        CanShip().ThrowIfFailure();

        Status = OrderStatus.Shipped;
        TrackingNumber = trackingNo;
        Raise(new OrderShipped(Id, trackingNo));
    }

    public Result CanCancel() => Status switch
    {
        OrderStatus.Shipped => Result.Failure(OrderErrors.ShippedCannotBeCancelled),
        OrderStatus.Cancelled => Result.Failure(OrderErrors.AlreadyCancelled),
        _ => Result.Success()
    };

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException(OrderErrors.CancellationReasonRequired);
        CanCancel().ThrowIfFailure();

        Status = OrderStatus.Cancelled;
        CancellationReason = reason;
        Raise(new OrderCancelled(Id, reason, SnapshotLines()));
    }

    private List<OrderPlacedLine> SnapshotLines() =>
        _lines.Select(l => new OrderPlacedLine(l.ProductId, l.Quantity)).ToList();
}
