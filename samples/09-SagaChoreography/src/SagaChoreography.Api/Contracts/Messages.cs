namespace SagaChoreography.Contracts;

// [Contracts] Servisler arası mesaj sözleşmeleri. Gerçek dağıtık sistemde bu klasör ayrı bir NuGet paketi
// (veya her serviste kopyalanmış sözleşme) olurdu. Hepsi EVENT: geçmiş zaman, "bu oldu" der, kimseye emir vermez.
// Choreography'de KOMUT yoktur; her servis bir olaya tepki verip yeni bir olay yayınlar.

public sealed record OrderLineItem(Guid ProductId, int Quantity);

// Ordering → (Inventory)                 T1: sipariş Pending olarak oluştu
public sealed record OrderCreated(Guid OrderId, Guid CustomerId, IReadOnlyList<OrderLineItem> Lines, decimal Total);

// Inventory → (Payment)                  T2: stok rezerve edildi
public sealed record StockReserved(Guid OrderId, Guid CustomerId, decimal Total);

// Inventory → (Ordering)                 T2 başarısız: siparişi reddet
public sealed record StockReservationFailed(Guid OrderId, string Reason);

// Payment → (Ordering)                   T3: ödeme alındı → sipariş onaylanır
public sealed record PaymentCompleted(Guid OrderId, Guid PaymentId, decimal Amount);

// Payment → (Ordering + Inventory)       T3 başarısız → COMPENSATION tetikler: Inventory stoğu iade eder, Ordering reddeder
public sealed record PaymentFailed(Guid OrderId, string Reason);

// Ordering → (Inventory + Payment)       Onaylanmış sipariş müşteri tarafından iptal edildi → stok iadesi + ödeme iadesi
public sealed record OrderCancelled(Guid OrderId, string Reason);
