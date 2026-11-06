using Clean.Domain.Common;

namespace Clean.Domain.Orders;

// Ubiquitous Language: iş kuralı ihlallerinin kataloğu. Kodlar API yanıtlarında da görünür.
public static class OrderErrors
{
    public const int MaxLines = 10;

    public static readonly Error NoLines = new("Order.NoLines", "Sipariş en az 1 kalem içermeli.");
    public static readonly Error TooManyLines = new("Order.TooManyLines", $"Sipariş en fazla {MaxLines} kalem içerebilir.");
    public static readonly Error InvalidQuantity = new("Order.InvalidQuantity", "Miktar 0'dan büyük olmalı.");
    public static readonly Error OnlyPlacedCanBeShipped = new("Order.OnlyPlacedCanBeShipped", "Sadece 'Placed' durumundaki sipariş kargolanabilir.");
    public static readonly Error ShippedCannotBeCancelled = new("Order.ShippedCannotBeCancelled", "Kargolanmış sipariş iptal edilemez.");
    public static readonly Error AlreadyCancelled = new("Order.AlreadyCancelled", "Sipariş zaten iptal edilmiş.");
    public static readonly Error TrackingNumberRequired = new("Order.TrackingNumberRequired", "Kargo takip numarası zorunludur.");
    public static readonly Error CancellationReasonRequired = new("Order.CancellationReasonRequired", "İptal nedeni zorunludur.");
}
