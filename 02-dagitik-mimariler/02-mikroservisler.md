# 12. Mikroservis Mimarisi

> 💻 **Çalışan örnek proje:** [`samples/10-Microservices`](../samples/10-Microservices/)

> Martin Fowler & James Lewis, 2014; Netflix, Amazon, Uber pratikleri

## Neden Ortaya Çıktı?

Büyüyen monolitlerde (ve SOA'da) şu sorunlar yaşandı:

- **Deploy darboğazı**: 200 geliştirici tek bir uygulamayı deploy ediyor; bir ekibin hatası herkesi bloke ediyor. Release ayda bir.
- **Ölçekleme verimsizliği**: Sadece "arama" yoğun, ama tüm uygulamayı 20 kopya çalıştırmak gerekiyor.
- **Teknoloji kilidi**: 10 yıllık monolit tek bir framework versiyonuna mahkûm.
- **Hata izolasyonu yok**: Rapor modülündeki memory leak ödeme sistemini çökertiyor.
- **Conway Yasası**: "Sistemler, onları tasarlayan organizasyonun iletişim yapısını yansıtır." Büyük organizasyon + tek kod tabanı = koordinasyon cehennemi.
- SOA'nın ESB ve paylaşılan DB sorunları.

## Çözdüğü Problem

> "Sistemi, iş yeteneklerine göre ayrılmış, **bağımsız deploy edilebilen**, **kendi verisine sahip**, küçük ve otonom servislere böl; böylece ekipler birbirinden bağımsız hızla hareket edebilsin."

**Asıl hedef teknik değil, organizasyoneldir: ekip otonomisi ve bağımsız deploy.**

## Yapı

```
                 ┌──────────────┐   ┌────────────┐
   Clients ────► │ API Gateway  │──►│ Identity   │
                 │ (YARP)       │   │ (Keycloak) │
                 └──┬────┬───┬──┘   └────────────┘
          ┌─────────┘    │   └──────────┐
          ▼              ▼              ▼
   ┌────────────┐ ┌────────────┐ ┌────────────┐
   │ Ordering   │ │ Catalog    │ │ Payment    │   Her biri:
   │ .NET 9     │ │ .NET 8     │ │ Go         │   - Ayrı repo/pipeline
   │ Clean+DDD  │ │ VSA        │ │            │   - Ayrı ekip
   └─────┬──────┘ └─────┬──────┘ └─────┬──────┘   - Bağımsız deploy
         │              │              │
     [SQL Srv]      [Postgres]     [Mongo]         ← Database per Service
         │              │              │
         └──────────────┴──── Message Broker (RabbitMQ / Kafka / ASB) ────┘
                                       │
                Observability: OpenTelemetry → Jaeger/Grafana/Seq
```

## Temel Özellikler (Fowler & Lewis)

1. **Servisler aracılığıyla bileşenleştirme**
2. **İş yetenekleri etrafında organizasyon** (bounded context, cross-functional ekipler)
3. **Proje değil ürün** ("You build it, you run it")
4. **Akıllı uçlar, aptal borular** (ESB yok; HTTP veya basit broker)
5. **Merkezi olmayan yönetişim** (her servis kendi teknolojisini seçebilir)
6. **Merkezi olmayan veri yönetimi** (database per service)
7. **Altyapı otomasyonu** (CI/CD, IaC, container)
8. **Hata için tasarım** (circuit breaker, retry, timeout)
9. **Evrimsel tasarım**

## Servis Sınırlarını Belirlemek

- **DDD Bounded Context** — en güvenilir yöntem.
- **İş yeteneği** (business capability): Sipariş, Stok, Fatura, Kargo.
- **Event Storming** atölyeleri.
- ❌ Teknik katmanlara göre bölme (`DataService`, `ValidationService`) — anti-pattern.
- ❌ Entity'ye göre bölme (`CustomerService`, `AddressService`) — chatty, aşırı bağımlı servisler.

## İletişim

| Tür | Teknoloji | Ne zaman |
|-----|-----------|----------|
| Senkron istek/yanıt | REST (HttpClient + Refit), gRPC | Anında yanıt gerekiyorsa (sorgu) |
| Asenkron mesaj | RabbitMQ, ASB, Kafka + MassTransit | Komutlar, olaylar, uzun süreçler |
| Olay akışı | Kafka, Event Hubs | Yüksek hacim, replay |

> Kural: Servisler arası **senkron çağrı zincirlerinden kaçının** (A→B→C→D). Her halka availability'yi çarpar: 0.99⁴ ≈ 0.96.

## .NET Ekosistemi

```csharp
// .NET Aspire AppHost — tüm sistemi lokal orkestre eder
var builder = DistributedApplication.CreateBuilder(args);

var rabbit   = builder.AddRabbitMQ("messaging");
var orderDb  = builder.AddPostgres("pg").AddDatabase("orderdb");
var catalogDb= builder.AddSqlServer("sql").AddDatabase("catalogdb");

var catalog  = builder.AddProject<Projects.Catalog_Api>("catalog").WithReference(catalogDb).WithReference(rabbit);
var ordering = builder.AddProject<Projects.Ordering_Api>("ordering").WithReference(orderDb).WithReference(rabbit)
                      .WithReference(catalog);   // service discovery

builder.AddProject<Projects.Gateway>("gateway").WithReference(catalog).WithReference(ordering);
builder.Build().Run();
```

```csharp
// Ordering servisinde Catalog'u çağırmak (service discovery + resilience)
builder.Services.AddHttpClient<CatalogClient>(c => c.BaseAddress = new("https+http://catalog"))
                .AddStandardResilienceHandler();

// Asenkron olay yayınlamak (MassTransit)
builder.Services.AddMassTransit(x =>
{
    x.AddConsumers(typeof(Program).Assembly);
    x.AddEntityFrameworkOutbox<OrderingDbContext>(o => { o.UsePostgres(); o.UseBusOutbox(); });
    x.UsingRabbitMq((ctx, cfg) => { cfg.Host(builder.Configuration.GetConnectionString("messaging")); cfg.ConfigureEndpoints(ctx); });
});
```

## Mikroservislerin Getirdiği Yeni Problemler (ve Çözen Desenler)

| Problem | Çözüm Deseni |
|---------|--------------|
| Servisler arası transaction | **[Saga](../03-dagitik-desenler/01-saga.md)** |
| DB yazma + mesaj yayını atomik değil | **[Outbox / Inbox](../03-dagitik-desenler/02-outbox-inbox.md)** |
| İstemci çok servisle konuşuyor | **[API Gateway / BFF](../03-dagitik-desenler/03-api-gateway-bff.md)** |
| Ağ hataları, kademeli çöküş | **[Retry, Circuit Breaker, Bulkhead](../03-dagitik-desenler/05-dayaniklilik-desenleri.md)** |
| Birden fazla servisten veri birleştirme | **API Composition, CQRS read model** |
| Legacy'den geçiş | **[Strangler Fig](../03-dagitik-desenler/04-strangler-fig.md)** |
| Hata ayıklama | **Distributed Tracing** (OpenTelemetry, correlation ID) |
| Servis bulma | **Service Discovery** (Aspire, Consul, K8s DNS) |
| Konfigürasyon | **Externalized Config** (Azure App Config, Consul, K8s ConfigMap) |
| Sözleşme kırılması | **Consumer-Driven Contract Testing** (Pact) |

## Artıları

- ✅ Bağımsız deploy → yüksek release hızı.
- ✅ Bağımsız ölçekleme.
- ✅ Hata izolasyonu.
- ✅ Teknoloji çeşitliliği (polyglot).
- ✅ Ekip otonomisi, net sahiplik.
- ✅ Küçük, anlaşılır kod tabanları.

## Eksileri

- ❌ **Dağıtık sistem karmaşıklığı** (ağ güvenilmez, gecikme var, saatler senkron değil).
- ❌ **Veri tutarlılığı**: ACID yok, eventual consistency.
- ❌ Operasyonel yük: K8s, service mesh, monitoring, log aggregation, tracing.
- ❌ Test etmek zor (entegrasyon, contract, E2E).
- ❌ Ağ gecikmesi ve serileştirme maliyeti.
- ❌ Yanlış sınırlar = **Distributed Monolith** (en kötü senaryo).

## Distributed Monolith Belirtileri 🚩

- Bir servisi deploy etmek için diğerlerini de deploy etmek gerekiyor.
- Servisler aynı veritabanını paylaşıyor.
- Ortak "Shared.Domain" NuGet paketi her değişiklikte tüm servislerde güncelleniyor.
- Bir istek 6+ servisten senkron geçiyor.
- Ekipler sürekli birbirini bekliyor.

## Ne Zaman Kullanılır?

- Büyük organizasyon (birden fazla bağımsız ekip)
- Bağımsız ölçekleme gerçek bir ihtiyaçsa
- Domain sınırları **iyi anlaşılmış**sa
- DevOps olgunluğu yüksekse (CI/CD, container, monitoring)

## Ne Zaman Kullanılmaz?

- Startup / MVP / küçük ekip → **Modüler Monolit** ile başlayın.
- Domain belirsizse.
- "Netflix yapıyor" diye.

> "Bir mikroservis mimarisi kurmak için önce iyi bir monolit kurabilmelisiniz. Monolitinizi modüler yapamıyorsanız, mikroservisleriniz de dağıtık bir çamur topu olur." — Simon Brown
