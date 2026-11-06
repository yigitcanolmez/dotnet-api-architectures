namespace Clean.Domain.Common;

// [Domain Katmanı] Domain event = "domain'de olmuş önemli bir şey" (geçmiş zaman ile adlandırılır).
// Bilinçli olarak MediatR'ın INotification'ını İMPLEMENTE ETMİYORUZ: Domain hiçbir pakete bağımlı değil.
// Application katmanı bu event'leri DomainEventNotification<T> ile sarmalayıp yayınlar.
public interface IDomainEvent;
