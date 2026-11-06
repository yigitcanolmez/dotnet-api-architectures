using ModularMonolith.Shared.Abstractions.Events;

namespace ModularMonolith.Modules.Catalog.Contracts;

// [Catalog.Contracts — Integration Events] Catalog'un dünyaya duyurduğu olgular (geçmiş zaman).

// Bir siparişin stoğu başarıyla ayrıldı.
public sealed record StockReservedIntegrationEvent(Guid OrderId) : IntegrationEvent;

// Stok yetersiz olduğu için sipariş için stok ayrılamadı (Ordering bunu dinleyip siparişi iptal eder).
public sealed record StockReservationFailedIntegrationEvent(Guid OrderId, string Reason) : IntegrationEvent;
