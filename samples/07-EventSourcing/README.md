# 07 — Event Sourcing (BankAccount)

📖 İlgili doküman: [Event Sourcing](../../01-uygulama-ici-mimariler/08-event-sourcing.md) · bağlam: [CQRS Seviye 4](../../01-uygulama-ici-mimariler/07-cqrs.md)

> **Neden ortak sipariş domain'i değil de BankAccount?**
> Event Sourcing'in değeri "**ne oldu?**" sorusunun "**ne durumda?**" kadar önemli olduğu yerde ortaya çıkar. Banka hesabı bunun en saf örneğidir: bakiye zaten yüzyıllardır bir **defterin (ledger)** toplamıdır, hiçbir kayıt silinmez, hata "düzeltme kaydı" ile giderilir. Ayrıca `Deposit/Withdraw` gibi çok sayıda küçük olay; "geçen ayın 15'inde bakiye neydi?" (temporal query) ve "neden 150?" (audit) soruları doğal olarak ortaya çıkar. Siparişte (3 durum değişikliği) bu faydalar zor görülür. Dokümandaki kod örneği de BankAccount'tur.

## Bu örnekte ne görüyorsunuz?

- **Durum değil olay saklanır**: tek kaynak `Events` tablosu (append-only). `Balance` hiçbir yerde "asıl veri" olarak tutulmaz.
- **Aggregate = event'lerin katlanması**: `BankAccount` komutlarda karar verir → event üretir → durum **sadece** `Apply` ile değişir; `Rehydrate` geçmişi yeniden oynatır.
- **Optimistic concurrency**: `UNIQUE(StreamId, Version)` + `expectedVersion` → `ConcurrencyException` → **409**.
- **Snapshot**: her 5 event'te bir durumun kopyası; yükleme "snapshot + sonraki event'ler".
- **Projeksiyon + catch-up subscription**: `ProjectionWorker`, `GlobalPosition` checkpoint'inden `ReadAllAsync` ile yeni event'leri okuyup `AccountBalances` tablosunu günceller.
- **Süper güçler**: `GET /accounts/{id}/events` (bedava audit), `GET /accounts/{id}/balance-at?timestamp=` (zaman yolculuğu), `POST /admin/projections/rebuild` (read model'i silip baştan üret).

## Tablo vs event akışı

| Geleneksel (durum saklama) | Event Sourcing (bu örnek) |
|---|---|
| `Accounts` tablosu: `{ Id, Owner, Balance: 150 }` | `Events` tablosu: `#1 AccountOpened{Ali}` · `#2 MoneyDeposited{200}` · `#3 MoneyWithdrawn{100}` · `#4 MoneyDeposited{50}` |
| `UPDATE Accounts SET Balance = 150` → önceki değer **kaybolur** | Sadece `INSERT` → hiçbir şey kaybolmaz; `Balance = Σ event` |
| "Neden 150?" → cevap yok (ayrı audit tablosu/trigger gerekir) | `GET /accounts/{id}/events` → cevap kendisi |
| "Dün saat 14:00'te bakiye?" → bilinmez | `GET /accounts/{id}/balance-at?timestamp=...` → replay |
| Yeni rapor → sadece bugünden itibaren veri | Yeni projeksiyon → **tüm geçmişten** üretilir (`rebuild`) |
| Eşzamanlılık: satır kilidi / rowversion | Eşzamanlılık: `expectedVersion` + `UNIQUE(StreamId, Version)` |
| Ad-hoc SQL sorgusu kolay | Ad-hoc sorgu zor → projeksiyon gerekir (CQRS zorunlu) |

## Klasör yapısı

```
07-EventSourcing/
├── EventSourcing.slnx
├── src/EventSourcing.Api/
│   ├── Program.cs                          ← event store, snapshot, projeksiyon, worker kaydı (port 5007)
│   ├── appsettings.json                    ← Snapshots:Every = 5, Projection:PollIntervalMs = 500
│   ├── EventSourcing.Api.http
│   ├── Domain/
│   │   ├── BankAccount.cs                  ← aggregate: Open/Deposit/Withdraw/Close, Apply, Rehydrate, Version, Uncommitted
│   │   ├── Events.cs                       ← AccountOpened, MoneyDeposited, MoneyWithdrawn, AccountClosed
│   │   └── DomainException.cs
│   ├── EventStore/
│   │   ├── IEventStore.cs                  ← AppendAsync / ReadStreamAsync / ReadAllAsync + StoredEvent + ConcurrencyException
│   │   ├── SqliteEventStore.cs             ← Events tablosu (GlobalPosition, StreamId, Version, Type, Data, Timestamp)
│   │   └── EventTypeRegistry.cs            ← "MoneyDeposited" ↔ typeof(MoneyDeposited) eşlemesi + JSON
│   ├── Snapshots/
│   │   └── SnapshotStore.cs                ← Snapshots tablosu (StreamId, Version, Data)
│   ├── Projections/
│   │   ├── AccountBalanceProjection.cs     ← AccountBalances read model + checkpoint + Rebuild
│   │   └── ProjectionWorker.cs             ← BackgroundService: catch-up subscription (polling)
│   ├── Accounts/
│   │   ├── BankAccountRepository.cs        ← stream yükle (snapshot + tail) / append + snapshot politikası
│   │   └── AccountEndpoints.cs             ← tüm HTTP endpoint'leri
│   └── Common/
│       └── ProblemExceptionHandler.cs      ← DomainException / ConcurrencyException → 409
└── tests/EventSourcing.Tests/
    ├── Domain/AggregateSpec.cs             ← Given(events) → When(command) → Then(events) mini DSL
    ├── Domain/BankAccountTests.cs          ← aggregate davranış testleri
    ├── EventStore/SqliteEventStoreTests.cs ← concurrency (sıralı + 10 paralel yazma), ReadAll
    └── Api/AccountsApiTests.cs             ← temporal query, snapshot, projeksiyon rebuild (WebApplicationFactory)
```

## Referans grafiği

Proje seviyesinde: `EventSourcing.Tests ──► EventSourcing.Api`. Klasörler arası:

```
 Accounts/AccountEndpoints ──► Accounts/BankAccountRepository ──► EventStore/IEventStore ◄── SqliteEventStore
          │                              │                             │
          │                              └──► Snapshots/SnapshotStore   │
          │                                                             ▼
          └──► Projections/AccountBalanceProjection ──► IEventStore.ReadAllAsync
                          ▲
                Projections/ProjectionWorker

 Hepsi ──► Domain (BankAccount, Events)        Domain ──✗──► hiçbir şey
```

**Neden bu yönde?** `Domain` saf C#'tır: event store'u, JSON'u, SQLite'ı bilmez — bu yüzden Given-When-Then testleri hiçbir altyapı olmadan çalışır. Event store yalnızca `IEvent` taşır, `BankAccount`'u tanımaz (her aggregate için aynı store). Projeksiyonlar aggregate'i değil **event'leri** okur; bu sayede aggregate'e dokunmadan yeni read model eklenebilir. Snapshot ve projeksiyon tabloları **türetilmiş** veridir: silinseler de event'lerden yeniden üretilir.

## Bir isteğin yolculuğu

> Bu örnekte sipariş yok; `POST /orders/{id}/cancel`'ın karşılığı olarak **`POST /accounts/{id}/withdraw`** (kural ihlali → 409 olabilen komut) izlenir.

1. `Accounts/AccountEndpoints.cs` → `Execute(...)`: `amount <= 0` ise 400; değilse repository'den yükler.
2. `Accounts/BankAccountRepository.cs` → `LoadAsync`: `Snapshots/SnapshotStore.cs`'ten son snapshot (ör. v5) + `EventStore/SqliteEventStore.cs` → `ReadStreamAsync("account-{id}", afterVersion: 5)`.
3. `EventStore/EventTypeRegistry.cs` → her satırın `Type` ("MoneyDeposited") ve `Data` (JSON) alanından gerçek event nesnesi oluşturulur.
4. `Domain/BankAccount.cs` → `FromSnapshot` + `Apply(...)` → bakiye ve `Version` (ör. 7) hesaplanır.
5. `Domain/BankAccount.cs` → `Withdraw(amount)`: kapalı mı? tutar > bakiye mi? → `DomainException` (→ `Common/ProblemExceptionHandler.cs` → **409**). Kurallar geçerse `MoneyWithdrawn` event'i **Raise** edilir (Apply + Uncommitted).
6. `Accounts/BankAccountRepository.cs` → `SaveAsync`: `AppendAsync(stream, expectedVersion: 7, [MoneyWithdrawn])`.
7. `EventStore/SqliteEventStore.cs` → `BEGIN IMMEDIATE`; `MAX(Version)` hâlâ 7 mi? Değilse `ConcurrencyException` (→ **409**). Evet ise `INSERT` (Version 8). Araya giren yazma `UNIQUE(StreamId, Version)` ile de yakalanır.
8. Versiyon 5'in katını geçtiyse (ör. 9 → 10) `SnapshotStore.SaveAsync`. HTTP **200** `{ balance, version }`.
9. **Asenkron**: `Projections/ProjectionWorker.cs` ≤500 ms içinde `ReadAllAsync(checkpoint)` ile yeni event'i görür → `AccountBalanceProjection` `AccountBalances.Balance`'ı günceller ve checkpoint'i **aynı transaction'da** ilerletir → `GET /accounts` listesi güncellenir.

## Çalıştırma

```bash
cd samples/07-EventSourcing
dotnet build
dotnet test
dotnet run --project src/EventSourcing.Api      # http://localhost:5007
```

> Öğretim amaçlı sadelik: **migration/EF yok**; tablolar başlangıçta `CREATE TABLE IF NOT EXISTS` ile oluşturulur (`eventsourcing.db`, WAL modu). Ham `Microsoft.Data.Sqlite` kullanıldı ki event store mekanizması çıplak görülsün.

### Demo: geçmişe dönük bakiye ve projeksiyonu yeniden oluşturma

```bash
B=http://localhost:5007; J='Content-Type: application/json'

ID=$(curl -s -X POST $B/accounts -H "$J" -d '{"owner":"Ali"}' | sed 's/.*"id":"\([^"]*\)".*/\1/')
curl -s -X POST $B/accounts/$ID/deposit  -H "$J" -d '{"amount":200}'; echo
T=$(date -u +%Y-%m-%dT%H:%M:%S.%NZ); sleep 1          # ← "o an": bakiye 200
curl -s -X POST $B/accounts/$ID/withdraw -H "$J" -d '{"amount":100}'; echo
curl -s -X POST $B/accounts/$ID/deposit  -H "$J" -d '{"amount":50}';  echo

# Bugünkü durum (event'lerden rehydrate) → balance 150, version 4
curl -s $B/accounts/$ID; echo

# Audit: "neden 150?"
curl -s $B/accounts/$ID/events; echo

# Zaman yolculuğu: T anındaki bakiye → 200 (version 2)
curl -s "$B/accounts/$ID/balance-at?timestamp=$T"; echo

# Kurallar: yetersiz bakiye → 409, eski versiyonla yazma → 409 Concurrency
curl -s -X POST $B/accounts/$ID/withdraw -H "$J" -d '{"amount":1000}'; echo
curl -s -X POST $B/accounts/$ID/deposit  -H "$J" -d '{"amount":5,"expectedVersion":2}'; echo

# Projeksiyon (≤500ms gecikmeli) → yeniden oluştur → aynı sonuç
sleep 1; curl -s $B/accounts; echo
curl -s -X POST $B/admin/projections/rebuild; echo     # {"replayedEvents":N}
curl -s $B/accounts; echo

# Kapat → kapalı hesaba işlem 409
curl -s -i -X POST $B/accounts/$ID/close | head -1
curl -s -X POST $B/accounts/$ID/deposit -H "$J" -d '{"amount":10}'; echo
```

İsterseniz `sqlite3 src/EventSourcing.Api/eventsourcing.db "DELETE FROM AccountBalances"` ile okuma modelini elle silin; `rebuild` sonrası her şey geri gelir. Aynısını `Events` tablosuna yapamazsınız — **tek doğruluk kaynağı** odur.

## Snapshot neden ve nasıl?

Bir hesabın 10.000 event'i varsa her `GET`/komutta 10.000 satırı okuyup `Apply` etmek pahalıdır. `BankAccountRepository.SaveAsync`, versiyon 5'in bir katını geçtiğinde `BankAccount.ToSnapshot()` sonucunu `Snapshots` tablosuna yazar; `LoadAsync` önce snapshot'ı, sonra sadece `Version > snapshot.Version` event'lerini okur. `GET /accounts/{id}` yanıtındaki `loadedFromSnapshotVersion` alanı bunu gösterir. Snapshot bir **önbellektir**: aggregate yapısı değişirse snapshot'ları silmek yeterlidir. Eşik (5) öğretim için küçük tutuldu; gerçekte yüzler/binler mertebesindedir — ve çoğu kısa ömürlü stream için hiç gerekmez.

## Deneyin

1. **Yeni projeksiyon, eski veri**: "günlük toplam yatırılan para" (`DailyDeposits`: Date, Total) projeksiyonu yazın ve kendi checkpoint'iyle çalıştırın. Siz kodu yazmadan **önce** yapılmış yatırmalar da raporda görünecek — ES'nin en büyük farkı.
2. **Şema evrimi (upcasting)**: `MoneyDeposited`'a `Description` alanı ekleyin. Eski JSON'larda bu alan yok; `EventTypeRegistry.Deserialize`'da varsayılan değer verin ya da `MoneyDeposited.v2` tip adı ile ayrı kayıt yapın.
3. **Kural ekleyin**: "Bakiyesi sıfır olmayan hesap kapatılamaz". Sadece `BankAccount.Close()` ve bir Given-When-Then testi değişmeli. Geçmişteki (kural öncesi) `AccountClosed` event'leri yine de oynatılabiliyor mu? Neden (Apply kural kontrolü yapmaz)?

## Bu örnekteki gözlemler

**Artılar**
- ✅ Audit, temporal query ve read model yeniden üretimi ekstra tablo/trigger olmadan "bedava".
- ✅ Aggregate testleri altyapısız ve son derece okunur (Given-When-Then).
- ✅ Append-only yazma basit; concurrency kontrolü tek bir UNIQUE kısıtıyla garanti.

**Eksiler**
- ❌ Basit bir bakiye için bile event store, registry, snapshot, projeksiyon, worker, checkpoint gerekti — CRUD'a göre çok daha fazla hareketli parça.
- ❌ `GET /accounts` (projeksiyon) eventually consistent; `GET /accounts/{id}` (rehydrate) ise anında tutarlı — istemci hangisini kullandığını bilmeli.
- ❌ Event şeması değişmez sözleşmedir; yanlış isimlendirilmiş bir event sonsuza kadar yaşar.
- ❌ Ad-hoc sorgu (ör. "bakiyesi 1000'den büyük hesaplar") ancak projeksiyon üzerinden yapılabilir. GDPR "unutulma hakkı" için crypto-shredding gibi ek teknikler gerekir.
