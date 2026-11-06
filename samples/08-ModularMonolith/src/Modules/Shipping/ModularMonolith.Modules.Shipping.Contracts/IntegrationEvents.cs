using ModularMonolith.Shared.Abstractions.Events;

namespace ModularMonolith.Modules.Shipping.Contracts;

// [Shipping.Contracts] Şu an bu event'i dinleyen modül YOK. Yarın bir "Notification" modülü eklendiğinde
// sadece bir handler yazması yeterli olur; Shipping'e dokunulmaz (EDA'nın gevşek bağlılık faydası).
public sealed record ShipmentCreatedIntegrationEvent(Guid ShipmentId, Guid OrderId) : IntegrationEvent;
