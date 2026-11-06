// [Paylaşılan Sözleşme — Command'lar] "Bunu yap" (emir kipi), TEK alıcı.
// Orkestratör (Ordering içindeki saga) bu komutları belirli bir kuyruğa Send eder.
// UYARI: Bu pakette domain/iş kuralı YOK; sadece serileştirilebilir veri taşıyan record'lar var.
namespace Contracts.Commands;

public record StockItem(Guid ProductId, int Quantity);

/// <summary>Inventory'ye: bu sipariş için stok ayır.</summary>
public record ReserveStock(Guid OrderId, IReadOnlyList<StockItem> Items);

/// <summary>Inventory'ye: ayrılmış stoğu geri bırak (COMPENSATION).</summary>
public record ReleaseStock(Guid OrderId, string Reason);

/// <summary>Payment'a: bu sipariş için tahsilat yap.</summary>
public record ProcessPayment(Guid OrderId, Guid CustomerId, decimal Amount);
