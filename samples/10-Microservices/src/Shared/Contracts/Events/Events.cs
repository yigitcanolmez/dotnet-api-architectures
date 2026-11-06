// [Paylaşılan Sözleşme — Event'ler] "Bu oldu" (geçmiş zaman), 0..N alıcı. Publish edilir.
// Integration event'ler public sözleşmedir: alan EKLEMEK güvenli, silmek/yeniden adlandırmak kırıcıdır.
namespace Contracts.Events;

public record OrderItem(Guid ProductId, int Quantity, decimal UnitPrice);

// Ordering → (saga başlatır). Event-carried state transfer: saga'nın ihtiyaç duyduğu her şey mesajda.
public record OrderSubmitted(Guid OrderId, Guid CustomerId, decimal Total, IReadOnlyList<OrderItem> Items, DateTime SubmittedAt);

// Inventory → saga
public record StockReserved(Guid OrderId);
public record StockReservationFailed(Guid OrderId, string Reason);

// Payment → saga
public record PaymentCompleted(Guid OrderId, Guid PaymentId, decimal Amount);
public record PaymentFailed(Guid OrderId, string Reason);

// Saga → herkes (Ordering kendi Order tablosunu bunlarla günceller)
public record OrderConfirmed(Guid OrderId);
public record OrderRejected(Guid OrderId, string Reason);

// Ordering → Inventory: onaylanmış sipariş iptal edildi, stok geri dönsün.
public record OrderCancelled(Guid OrderId, string Reason);
