# 6. Domain-Driven Design (DDD)

> 💻 **Çalışan örnek proje:** [`samples/04-CleanArchitecture`](../samples/04-CleanArchitecture/)

> Eric Evans, "Domain-Driven Design: Tackling Complexity in the Heart of Software", 2003

## Neden Ortaya Çıktı?

Yazılım projelerinin çoğu teknik nedenlerle değil, **iş alanını yanlış anlamaktan** başarısız oluyordu:

- Geliştiriciler "tablo, kolon, servis" dili konuşuyor, iş birimi "poliçe, hasar, muafiyet" dili konuşuyor → **çeviri kaybı**.
- İş kuralları kod tabanına dağılmış; kimse "bu kural nerede?" sorusunu cevaplayamıyor.
- Büyük sistemlerde tek bir "Customer" modeli her departmanın ihtiyacını karşılamaya çalışıp **devasa ve tutarsız** hale geliyor.
- Karmaşıklık arttıkça "Big Ball of Mud" kaçınılmaz oluyor.

## Çözdüğü Problem

> "Karmaşık iş alanlarındaki (domain) karmaşıklığı, iş uzmanlarıyla birlikte geliştirilen ortak bir dil ve model üzerinden yönet."

DDD bir **mimari değildir**; bir **tasarım yaklaşımıdır**. İki parçası vardır:

## A. Stratejik Tasarım (Büyük resim — en önemli kısım)

### Ubiquitous Language (Ortak Dil)
İş uzmanı ve geliştirici **aynı terimleri** kullanır; kod da bu dili yansıtır.
```csharp
// ❌ order.Status = 3; repo.Update(order);
// ✅ order.Ship(trackingNumber);
```

### Bounded Context (Sınırlı Bağlam)
Bir modelin **geçerli olduğu sınır**. "Ürün" kelimesi Katalog'da (açıklama, görseller), Stok'ta (adet, raf), Fatura'da (KDV, fiyat) farklı anlamlara gelir. Her biri **ayrı bir model**dir.

```
┌──────────────┐   ┌──────────────┐   ┌──────────────┐
│   Catalog    │   │  Inventory   │   │   Billing    │
│ Product:     │   │ StockItem:   │   │ LineItem:    │
│  - Name      │   │  - Sku       │   │  - Sku       │
│  - Images    │   │  - Quantity  │   │  - Price     │
│  - Desc      │   │  - Location  │   │  - TaxRate   │
└──────────────┘   └──────────────┘   └──────────────┘
```

> Bounded Context'ler **mikroservis** veya **modüler monolit modülü** sınırlarını belirlemenin en iyi yoludur.

### Subdomain Türleri
| Tür | Açıklama | Yatırım |
|-----|----------|---------|
| **Core** | Rekabet avantajı sağlayan kısım | En iyi ekip, DDD taktikleri |
| **Supporting** | Gerekli ama özel değil | Basit mimari, CRUD |
| **Generic** | Herkesin ihtiyacı (auth, ödeme) | Satın al / hazır kullan |

### Context Mapping (Bağlamlar arası ilişkiler)
- **Shared Kernel**: İki bağlam ortak küçük bir model paylaşır.
- **Customer–Supplier**: Upstream, downstream'in ihtiyaçlarını gözetir.
- **Conformist**: Downstream, upstream modeline olduğu gibi uyar.
- **Anti-Corruption Layer (ACL)**: Downstream, upstream modelini kendi diline çeviren bir katman kurar.
- **Open Host Service / Published Language**: Upstream iyi dokümante edilmiş bir API/şema sunar.
- **Separate Ways**: Entegre olma, ayrı çöz.

## B. Taktik Tasarım (Kod seviyesi yapı taşları)

| Yapı taşı | Tanım | .NET'te |
|-----------|-------|---------|
| **Entity** | Kimliği olan, zamanla değişen nesne | `class` + `Id` |
| **Value Object** | Kimliği yok, değerleriyle eşit, değişmez | `record` / `readonly record struct` |
| **Aggregate** | Tutarlılık sınırı olan entity kümesi | Kök entity + iç entity'ler |
| **Aggregate Root** | Aggregate'e tek giriş noktası | Dışarıdan sadece root'a referans |
| **Domain Event** | Domain'de olmuş önemli bir şey | `record OrderShipped(...)` |
| **Domain Service** | Tek bir entity'ye ait olmayan domain mantığı | Stateless sınıf |
| **Repository** | Aggregate'leri saklama/getirme soyutlaması | Aggregate başına bir tane |
| **Factory** | Karmaşık oluşturma mantığı | Static `Create` metodu |
| **Specification** | Yeniden kullanılabilir iş kuralı/sorgu | `Ardalis.Specification` |

### Kod Örneği

```csharp
// Value Object
public sealed record Money(decimal Amount, string Currency)
{
    public static Money operator +(Money a, Money b) =>
        a.Currency == b.Currency ? new(a.Amount + b.Amount, a.Currency)
                                 : throw new DomainException("Para birimi uyuşmuyor");
    public static Money Zero(string c) => new(0, c);
}

// Aggregate Root
public sealed class Order : AggregateRoot<OrderId>
{
    private readonly List<OrderLine> _lines = [];
    public CustomerId CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
    public Money Total => _lines.Aggregate(Money.Zero("TRY"), (s, l) => s + l.SubTotal);

    private Order() { } // EF

    public static Order Place(CustomerId customer, IEnumerable<(ProductId, Money, int)> items)
    {
        var order = new Order { Id = OrderId.New(), CustomerId = customer, Status = OrderStatus.Placed };
        foreach (var (p, price, qty) in items) order.AddLine(p, price, qty);
        if (order._lines.Count == 0) throw new DomainException("Sipariş boş olamaz");
        order.Raise(new OrderPlaced(order.Id, customer, order.Total));
        return order;
    }

    public void AddLine(ProductId product, Money price, int qty)
    {
        if (Status != OrderStatus.Placed) throw new DomainException("Sipariş değiştirilemez");
        if (_lines.Count >= 50) throw new DomainException("En fazla 50 kalem");   // invariant
        _lines.Add(new OrderLine(product, price, qty));
    }

    public void Ship(string trackingNo)
    {
        if (Status != OrderStatus.Paid) throw new DomainException("Ödenmemiş sipariş kargolanamaz");
        Status = OrderStatus.Shipped;
        Raise(new OrderShipped(Id, trackingNo));
    }
}

// Strongly-typed ID
public readonly record struct OrderId(Guid Value) { public static OrderId New() => new(Guid.NewGuid()); }

// Base
public abstract class AggregateRoot<TId>
{
    private readonly List<IDomainEvent> _events = [];
    public TId Id { get; protected set; } = default!;
    public IReadOnlyList<IDomainEvent> DomainEvents => _events;
    protected void Raise(IDomainEvent e) => _events.Add(e);
    public void ClearEvents() => _events.Clear();
}
```

### Domain Event'leri yayınlamak (EF Core interceptor)

```csharp
public class DispatchDomainEventsInterceptor(IPublisher publisher) : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e, int result, CancellationToken ct)
    {
        var aggregates = e.Context!.ChangeTracker.Entries<IHasDomainEvents>().Select(x => x.Entity).ToList();
        var events = aggregates.SelectMany(a => a.DomainEvents).ToList();
        aggregates.ForEach(a => a.ClearEvents());
        foreach (var ev in events) await publisher.Publish(ev, ct);
        return result;
    }
}
```

### Aggregate Tasarım Kuralları (Vaughn Vernon)
1. **Gerçek invariant'ları** tutarlılık sınırı içinde modelle.
2. **Küçük aggregate'ler** tasarla.
3. Diğer aggregate'lere **sadece ID ile** referans ver.
4. Aggregate'ler arası tutarlılık için **eventual consistency** (domain event) kullan.
5. Bir transaction'da **tek bir aggregate** değiştir.

## Artıları

- ✅ İş kuralları tek yerde, açık ve test edilebilir.
- ✅ İş birimiyle ortak dil → daha az yanlış anlaşılma.
- ✅ Bounded Context'ler doğal servis/modül sınırları verir.
- ✅ Karmaşıklık kontrol altında büyür.

## Eksileri

- ❌ Öğrenme eğrisi yüksek.
- ❌ Domain uzmanına erişim şart.
- ❌ Basit CRUD alanlarında gereksiz maliyet.
- ❌ ORM ile zengin model eşlemesi bazen zorlayıcı (private setter, backing field, owned types).

## Ne Zaman Kullanılır?

- **Core domain**'de, iş kuralları karmaşıksa
- Uzun ömürlü ürünler
- Mikroservis/modül sınırlarını belirlerken (stratejik DDD her zaman faydalıdır)

> İpucu: Stratejik DDD'yi (bounded context, ortak dil) her projede düşünün; taktik DDD'yi (aggregate, VO, event) sadece karmaşık alt-alanlarda uygulayın.
