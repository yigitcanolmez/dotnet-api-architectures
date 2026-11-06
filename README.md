# .NET API Mimarileri — Kapsamlı Rehber

Bu doküman seti, bir .NET Web API geliştirirken karşılaşacağınız **tüm yaygın mimari stilleri ve desenleri** anlatır:
**neden ortaya çıktılar, hangi problemi çözüyorlar, nasıl uygulanırlar, ne zaman kullanılmamalılar.**

> Altın kural: Mimari bir **amaç değil, araçtır**. Her mimari bir problemi çözerken yeni bir maliyet (karmaşıklık) getirir.
> Doğru mimari = Sizin problemlerinizi çözen **en basit** mimari.

---

## 📚 İçindekiler

### 1. Uygulama İçi (Kod Organizasyonu) Mimarileri
Tek bir deploy edilebilir uygulamanın *içini* nasıl organize edersiniz?

| # | Mimari | Tek cümlede |
|---|--------|-------------|
| 1 | [Katmanlı Mimari (N-Tier / Layered)](01-uygulama-ici-mimariler/01-katmanli-mimari.md) | Kodu teknik sorumluluğa göre yatay katmanlara böl. |
| 2 | [Onion (Soğan) Mimarisi](01-uygulama-ici-mimariler/02-onion-mimarisi.md) | Domain merkezde, bağımlılıklar hep içe doğru. |
| 3 | [Hexagonal (Ports & Adapters)](01-uygulama-ici-mimariler/03-hexagonal-mimari.md) | Uygulama çekirdeği dış dünyayla sadece port'lar üzerinden konuşur. |
| 4 | [Clean Architecture](01-uygulama-ici-mimariler/04-clean-architecture.md) | Onion + Hexagonal + Use Case'lerin sentezi. |
| 5 | [Vertical Slice Architecture (VSA)](01-uygulama-ici-mimariler/05-vertical-slice-architecture.md) | Kodu teknik katmana göre değil, *özelliğe (feature)* göre böl. |
| 6 | [Domain-Driven Design (DDD)](01-uygulama-ici-mimariler/06-domain-driven-design.md) | Karmaşık iş kurallarını iş diliyle modelle. |
| 7 | [CQRS](01-uygulama-ici-mimariler/07-cqrs.md) | Okuma ve yazma modellerini ayır. |
| 8 | [Event Sourcing](01-uygulama-ici-mimariler/08-event-sourcing.md) | Durumu değil, durumu değiştiren olayları sakla. |
| 9 | [Modüler Monolit](01-uygulama-ici-mimariler/09-moduler-monolit.md) | Tek deploy, ama içeride bağımsız modüller. |
| 10 | [Minimal API / REPR Deseni](01-uygulama-ici-mimariler/10-minimal-api-repr.md) | Request–Endpoint–Response; endpoint başına bir sınıf. |

### 2. Dağıtık (Sistem Seviyesi) Mimariler
Birden fazla uygulama/servis birbiriyle nasıl çalışır?

| # | Mimari | Tek cümlede |
|---|--------|-------------|
| 11 | [Service-Oriented Architecture (SOA)](02-dagitik-mimariler/01-soa.md) | Kurumsal yetenekleri paylaşılan servisler olarak sun (ESB ile). |
| 12 | [Mikroservis Mimarisi](02-dagitik-mimariler/02-mikroservisler.md) | Bağımsız deploy edilen, kendi verisine sahip küçük servisler. |
| 13 | [Event-Driven Architecture (EDA)](02-dagitik-mimariler/03-event-driven-architecture.md) | Servisler birbirini çağırmaz, olay yayınlar/dinler. |
| 14 | [Serverless Mimari](02-dagitik-mimariler/04-serverless.md) | Sunucuyu düşünme; fonksiyon/olay bazlı çalıştır. |

### 3. Dağıtık Sistem Desenleri
Dağıtık mimarilerin doğurduğu problemleri çözen desenler.

| # | Desen | Çözdüğü problem |
|---|-------|-----------------|
| 15 | [Saga (Orchestration & Choreography)](03-dagitik-desenler/01-saga.md) | Servisler arası dağıtık transaction. |
| 16 | [Transactional Outbox & Inbox](03-dagitik-desenler/02-outbox-inbox.md) | DB'ye yazıp mesaj yayınlamanın atomik olmaması. |
| 17 | [API Gateway & BFF](03-dagitik-desenler/03-api-gateway-bff.md) | İstemcinin onlarca servisle konuşması. |
| 18 | [Strangler Fig](03-dagitik-desenler/04-strangler-fig.md) | Legacy sistemi adım adım taşımak. |
| 19 | [Dayanıklılık Desenleri (Retry, Circuit Breaker, Bulkhead...)](03-dagitik-desenler/05-dayaniklilik-desenleri.md) | Ağ ve bağımlı servis hataları. |
| 20 | [Database-per-Service, API Composition, Anti-Corruption Layer](03-dagitik-desenler/06-veri-ve-entegrasyon-desenleri.md) | Dağıtık veri sahipliği ve entegrasyon. |

### 💻 [Örnek Projeler](samples/README.md)
Her mimari için derlenen, test edilen ve çalışan .NET 10 örnek projeleri — aynı sipariş domain'i ile katmanları yan yana karşılaştırın.

### 4. [Karşılaştırma ve Karar Rehberi](04-karsilastirma-ve-karar-rehberi.md)
Hangi projede hangisi? Karar ağacı, kombinasyonlar, anti-pattern'ler.

---

## 🧭 Büyük Resim: Tarihsel Evrim

```
1990'lar   Monolit / Spagetti kod  ──►  "Her şey her yerde"
   │
2000'ler   Katmanlı (N-Tier)       ──►  Sorumlulukları ayır (UI / BLL / DAL)
   │       SOA + ESB               ──►  Kurumsal sistemleri entegre et
   │
2003       DDD (Eric Evans)        ──►  Karmaşık iş mantığını modelle
2005       Hexagonal (Cockburn)    ──►  Altyapıdan bağımsız çekirdek
2008       Onion (Palermo)         ──►  Domain merkezde, bağımlılık içe
2010       CQRS (Greg Young)       ──►  Okuma/yazma ayrımı
           Event Sourcing          ──►  Olayları sakla
2012       Clean Architecture      ──►  Hepsinin sentezi (Uncle Bob)
2014       Mikroservisler          ──►  Bağımsız deploy & ölçekleme
           Event-Driven            ──►  Gevşek bağlılık
2018       Vertical Slice (Bogard) ──►  Katman yerine özellik
2020+      Modüler Monolit         ──►  "Mikroservis çok erkendi" geri dönüşü
           Minimal API / REPR      ──►  Daha az tören, daha çok iş
```

### İki eksen var, karıştırmayın

1. **Kod organizasyonu (uygulama içi):** Layered, Onion, Hexagonal, Clean, VSA, DDD, CQRS
2. **Dağıtım topolojisi (sistem seviyesi):** Monolit, Modüler Monolit, SOA, Mikroservis, Serverless

Bu iki eksen **birbirinden bağımsızdır**. Örneğin:
- Bir **mikroservisin içi** VSA ile yazılabilir.
- Bir **monolit** Clean Architecture + DDD + CQRS ile yazılabilir.
- **Saga**, **Outbox** gibi desenler ise sadece dağıtık (veya modüler) sistemlerde gerekir.

---

## .NET Ekosisteminde Sık Kullanılan Araçlar

| Amaç | Kütüphane |
|------|-----------|
| Mediator / CQRS | MediatR, Mediator (source-gen), Wolverine, Brighter |
| Validasyon | FluentValidation |
| ORM | EF Core, Dapper, Marten (PostgreSQL doküman/event store) |
| Event Store | EventStoreDB (KurrentDB), Marten |
| Mesajlaşma | MassTransit, NServiceBus, Wolverine, Rebus, CAP |
| Broker | RabbitMQ, Azure Service Bus, Kafka, Amazon SQS/SNS |
| Saga | MassTransit State Machine, NServiceBus Saga, Wolverine Saga, Temporal, Dapr Workflow |
| API Gateway | YARP, Ocelot, Azure APIM |
| Dayanıklılık | Polly, Microsoft.Extensions.Http.Resilience |
| Gözlemlenebilirlik | OpenTelemetry, Serilog, Seq, Jaeger, Grafana |
| Orkestrasyon | .NET Aspire, Dapr, Kubernetes |
| Mimari testler | NetArchTest, ArchUnitNET |
| Endpoint | Minimal API, FastEndpoints, Carter |
