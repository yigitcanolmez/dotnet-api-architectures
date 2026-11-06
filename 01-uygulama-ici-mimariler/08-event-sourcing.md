# 8. Event Sourcing

> 💻 **Çalışan örnek proje:** [`samples/07-EventSourcing`](../samples/07-EventSourcing/)

> Greg Young / Martin Fowler; muhasebe defterleri kadar eski bir fikir

## Neden Ortaya Çıktı?

Geleneksel sistemler **son durumu** saklar (`UPDATE Accounts SET Balance = 150`). Önceki durum **kaybolur**. Sorunlar:

- "Bu bakiye neden 150?" sorusunun cevabı yok.
- Denetim (audit) için ayrı log tabloları, trigger'lar gerekir — ve bunlar eksik/tutarsız olabilir.
- Geçmişe dönük yeni bir rapor/analiz ihtiyacı çıktığında veri zaten yok.
- Hata ayıklama: "Kullanıcı bu duruma nasıl geldi?" yeniden üretilemez.
- Bankacılık, muhasebe gibi alanlar zaten yüzyıllardır **defter** (ledger) tutar: kayıt silinmez, düzeltme kaydı eklenir.

## Çözdüğü Problem

> "Durumu değil, durumu değiştiren **olayları** (event) değişmez ve sıralı biçimde sakla. Mevcut durum, olayların yeniden oynatılmasıyla (replay) elde edilir."

## Nasıl Çalışır?

```
Geleneksel:        Account { Id: 1, Balance: 150 }

Event Sourced:     Stream "account-1"
                   #1 AccountOpened   { Owner: "Ali" }
                   #2 MoneyDeposited  { Amount: 200 }
                   #3 MoneyWithdrawn  { Amount: 100 }
                   #4 MoneyDeposited  { Amount: 50 }
                   ─────────────────────────────────
                   Replay ⇒ Balance = 150
```

```
Command ─► Aggregate.Load(events) ─► Karar ver ─► Yeni event(ler) ─► Event Store (append-only)
                                                                        │
                                                    ┌───────────────────┼─────────────────┐
                                                    ▼                   ▼                 ▼
                                             Read Model #1       Read Model #2     Integration
                                             (SQL tablo)         (Elastic)         (Kafka'ya yayın)
```

## .NET Kod Örneği

```csharp
// Events
public interface IEvent;
public record AccountOpened(Guid Id, string Owner) : IEvent;
public record MoneyDeposited(Guid Id, decimal Amount) : IEvent;
public record MoneyWithdrawn(Guid Id, decimal Amount) : IEvent;

// Aggregate
public class BankAccount
{
    public Guid Id { get; private set; }
    public decimal Balance { get; private set; }
    public int Version { get; private set; }
    private readonly List<IEvent> _uncommitted = [];
    public IReadOnlyList<IEvent> Uncommitted => _uncommitted;

    public static BankAccount Open(Guid id, string owner)
    {
        var acc = new BankAccount();
        acc.Raise(new AccountOpened(id, owner));
        return acc;
    }

    public void Withdraw(decimal amount)
    {
        if (amount > Balance) throw new DomainException("Yetersiz bakiye");   // karar
        Raise(new MoneyWithdrawn(Id, amount));
    }

    public void Deposit(decimal amount) => Raise(new MoneyDeposited(Id, amount));

    // Durum SADECE Apply ile değişir
    private void Apply(IEvent e)
    {
        switch (e)
        {
            case AccountOpened x: Id = x.Id; break;
            case MoneyDeposited x: Balance += x.Amount; break;
            case MoneyWithdrawn x: Balance -= x.Amount; break;
        }
        Version++;
    }

    private void Raise(IEvent e) { Apply(e); _uncommitted.Add(e); }

    public static BankAccount Rehydrate(IEnumerable<IEvent> history)
    {
        var acc = new BankAccount();
        foreach (var e in history) acc.Apply(e);
        return acc;
    }
}
```

### Marten ile (PostgreSQL)

```csharp
builder.Services.AddMarten(o =>
{
    o.Connection(cs);
    o.Projections.Add<AccountBalanceProjection>(ProjectionLifecycle.Inline);
});

// Command handler
app.MapPost("/accounts/{id}/withdraw", async (Guid id, decimal amount, IDocumentSession session) =>
{
    var stream = await session.Events.FetchForWriting<BankAccount>(id); // optimistic concurrency
    stream.Aggregate.Withdraw(amount);
    stream.AppendMany(stream.Aggregate.Uncommitted);
    await session.SaveChangesAsync();
});

// Projeksiyon (read model)
public class AccountBalanceProjection : SingleStreamProjection<AccountBalance>
{
    public AccountBalance Create(AccountOpened e) => new(e.Id, e.Owner, 0);
    public AccountBalance Apply(MoneyDeposited e, AccountBalance s) => s with { Balance = s.Balance + e.Amount };
    public AccountBalance Apply(MoneyWithdrawn e, AccountBalance s) => s with { Balance = s.Balance - e.Amount };
}
```

## Önemli Kavramlar

| Kavram | Açıklama |
|--------|----------|
| **Stream** | Bir aggregate'in event dizisi (`account-1`). |
| **Event Store** | Append-only, stream bazlı, sıralı event deposu (EventStoreDB/KurrentDB, Marten, Cosmos DB). |
| **Projection** | Event'lerden okuma modeli üretme (inline, async, live). |
| **Snapshot** | Uzun stream'lerde her N event'te durumu kaydetmek; replay hızlandırır. |
| **Optimistic Concurrency** | `expectedVersion` ile çakışan yazmaları engellemek. |
| **Upcasting** | Eski event şemalarını yeni versiyona dönüştürme. |
| **Temporal Query** | "Geçen ayın 15'inde bakiye neydi?" |

## Artıları

- ✅ Tam ve güvenilir **audit log** (bedava).
- ✅ Zaman yolculuğu: herhangi bir andaki durumu yeniden üret.
- ✅ Yeni read model'ler geçmiş veriden **sonradan** üretilebilir.
- ✅ Hata ayıklama: olayları replay et.
- ✅ Event-driven mimari ile doğal entegrasyon.
- ✅ Append-only yazma → yüksek yazma performansı.

## Eksileri

- ❌ Yüksek karmaşıklık ve öğrenme eğrisi.
- ❌ **Event şema evrimi** zor (event'ler değişmez, sonsuza kadar yaşar).
- ❌ Okuma için CQRS + projeksiyon zorunlu; eventual consistency.
- ❌ GDPR "unutulma hakkı" → crypto-shredding gibi teknikler gerekir.
- ❌ Ad-hoc sorgulama zor (event store'a SQL yazamazsınız).
- ❌ Ekip deneyimi yoksa felaket olabilir.

## Ne Zaman Kullanılır?

- Finans, muhasebe, sigorta, lojistik takip, oyun durumu
- Audit/regülasyon zorunluysa
- "Ne oldu?" sorusu "ne durumda?" kadar önemliyse
- **Sistemin tamamına değil**, sadece ihtiyaç duyan bounded context'e uygulayın.

## Ne Zaman Kullanılmaz?

- Basit CRUD
- Ekip CQRS/DDD deneyimsizse
- "Sadece audit log lazım" ise → basit bir audit tablosu/EF interceptor yeterli
