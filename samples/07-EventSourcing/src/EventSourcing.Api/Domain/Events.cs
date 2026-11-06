namespace EventSourcing.Api.Domain;

// [Domain / Events] Olaylar GEÇMİŞ ZAMANDA isimlendirilir ve DEĞİŞMEZ (immutable record).
// Bir kez yazılan event sonsuza kadar yaşar → alan eklemek/çıkarmak "şema evrimi" (upcasting) gerektirir.
// Zaman damgası event'in içinde değil, event store metadata'sında tutulur.
public interface IEvent;

public sealed record AccountOpened(Guid AccountId, string Owner) : IEvent;

public sealed record MoneyDeposited(Guid AccountId, decimal Amount) : IEvent;

public sealed record MoneyWithdrawn(Guid AccountId, decimal Amount) : IEvent;

public sealed record AccountClosed(Guid AccountId) : IEvent;
