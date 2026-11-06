# Karşılaştırma ve Karar Rehberi

## 1. Hangi Mimari Hangi Problemi Çözüyor? (Özet Tablo)

| Mimari / Desen | Çözdüğü Ana Problem | Getirdiği Maliyet |
|----------------|---------------------|-------------------|
| Katmanlı | Kodun karmakarışık olması | İş mantığı DB'ye bağımlı, anemic model |
| Onion | İş mantığının altyapıya bağımlılığı | Fazla soyutlama, çok proje |
| Hexagonal | Uygulamanın tek bir giriş/çıkış teknolojisine kilitlenmesi | Port/adapter sayısı |
| Clean | Yukarıdakilerin sentezi + use case görünürlüğü | Tören, mapping, yatay dağılım |
| VSA | Bir özelliğin tüm katmanlara dağılması, paylaşılan soyutlamaların coupling'i | Disiplin, tekrar riski |
| DDD | Karmaşık iş alanının yanlış anlaşılması/modellenmesi | Öğrenme eğrisi, domain uzmanı ihtiyacı |
| CQRS | Okuma ve yazma ihtiyaçlarının çatışması | Daha çok kod; ileri seviyede eventual consistency |
| Event Sourcing | Geçmişin kaybolması, audit | Yüksek karmaşıklık, şema evrimi |
| Modüler Monolit | Monolitte çamur topu / mikroserviste dağıtık karmaşıklık | Bağımsız ölçekleme yok |
| Minimal API / REPR | Şişman controller, tören | Program.cs disiplini |
| SOA | Kurumsal heterojen sistemlerin entegrasyonu | ESB darboğazı, ağır standartlar |
| Mikroservis | Organizasyonel ölçek, bağımsız deploy/ölçekleme | Dağıtık sistem karmaşıklığı |
| EDA | Temporal ve bilgi coupling'i | İzlenebilirlik, eventual consistency |
| Serverless | Altyapı yönetimi, düzensiz yük maliyeti | Cold start, vendor lock-in |
| Saga | Dağıtık transaction | Telafi mantığı, izolasyon eksikliği |
| Outbox/Inbox | Dual write, duplicate mesaj | Ek tablo ve processor |
| API Gateway/BFF | İstemci–servis karmaşası | Ek bileşen, SPOF riski |
| Strangler Fig | Riskli big-bang rewrite | Paralel iki sistem |
| Resilience | Kademeli çöküş | Konfigürasyon karmaşıklığı |

## 2. İki Eksen — Kombinasyonlar

```
                        KOD ORGANİZASYONU (içerisi)
                 Layered   Clean/Onion   VSA     VSA+DDD   Clean+DDD+CQRS
  D  Monolit       ✅ CRUD    ✅           ✅        ✅          ✅
  A  Mod. Monolit  ⚠️        ✅           ✅✅      ✅✅        ✅
  Ğ  Mikroservis   ⚠️        ✅           ✅✅      ✅✅        ✅ (core domain)
  I  Serverless    ✅        ⚠️ (ağır)    ✅        ✅          ⚠️
  T
  I  ✅✅ = çok uygun, ✅ = uygun, ⚠️ = dikkat
  M
```

**Günümüzde popüler ve dengeli kombinasyon:**
> **Modüler Monolit** + her modülde **VSA** + core modüllerde **DDD** + **CQRS (Seviye 1)** + modüller arası **Integration Events + Outbox** + gerekiyorsa **Saga**.

## 3. Karar Ağacı

```
Proje başlıyor
│
├─ Basit CRUD / admin paneli / prototip mi?
│     └─ EVET → Minimal API + (Katmanlı veya basit VSA). Bitti.
│
├─ İş kuralları karmaşık mı?
│     ├─ EVET → DDD (taktik) + Clean/Onion veya VSA+Domain
│     └─ HAYIR → VSA
│
├─ Okuma/yazma ihtiyaçları belirgin farklı mı?
│     └─ EVET → CQRS Seviye 1 (gerekirse 2-3)
│
├─ Audit / "ne oldu" geçmişi kritik mi? (finans, sigorta)
│     └─ EVET → İlgili bounded context'te Event Sourcing
│
├─ Dağıtım:
│     ├─ Ekip < ~30, domain sınırları net değil → MODÜLER MONOLİT
│     ├─ Birden fazla bağımsız ekip, bağımsız ölçek/deploy şart,
│     │  DevOps olgun, sınırlar net → MİKROSERVİS
│     ├─ Düzensiz yük, olay tetiklemeli işler → SERVERLESS (kısmen)
│     └─ Heterojen kurumsal sistem entegrasyonu → SOA / Integration Platform
│
└─ Dağıtık (mikroservis veya ayrı DB'li modüller) ise ŞUNLAR ZORUNLU:
      ├─ Outbox + Idempotent Consumer (Inbox)
      ├─ Birden çok servise yayılan işlem → Saga
      │     ├─ 2-4 adım, basit → Choreography
      │     └─ 4+ adım, timeout, durum takibi → Orchestration
      ├─ API Gateway (+ istemci başına BFF gerekirse)
      ├─ Resilience (timeout, retry, circuit breaker)
      └─ Observability (OpenTelemetry, correlation ID, merkezi log)

Legacy'den geçiş mi? → Strangler Fig + Anti-Corruption Layer
```

## 4. Evrimsel Yol (Önerilen)

```
 1. Monolit (VSA)          → hızlı başla, değer üret
        │  özellikler arttı, sınırlar belirginleşti
 2. Modüler Monolit        → bounded context'lere göre modüller, ayrı schema
        │  modüller arası async olaylar + outbox
 3. Seçici Ayrıştırma      → sadece bağımsız ölçek/deploy GERÇEKTEN gereken modülü
        │                    mikroservise çıkar (Strangler Fig)
 4. Mikroservis (gerekirse)
```

Her adımda geri dönülebilirlik korunur; erken ve yanlış servis sınırlarının bedeli ödenmez.

## 5. Yaygın Anti-Pattern'ler 🚫

| Anti-pattern | Belirti | Çözüm |
|--------------|---------|-------|
| **Big Ball of Mud** | Her şey her şeye bağlı | Modülerleştir, sınır çiz |
| **Anemic Domain Model** | Entity'ler sadece property, mantık servislerde | Davranışı entity'ye taşı (gerekiyorsa) |
| **Distributed Monolith** | Servisler birlikte deploy ediliyor | Sınırları yeniden çiz, async iletişim |
| **Shared Database** | Servisler aynı tablolara yazıyor | Database per service |
| **Generic Repository over EF Core** | `IRepository<T>` her şeyi sarar, IQueryable sızdırır | DbContext'i doğrudan kullan veya aggregate'e özel repository |
| **Mikroservis-first** | 3 kişi, 20 servis | Modüler monolitle başla |
| **God Orchestrator / God ESB** | Tüm iş mantığı orkestratörde/ESB'de | Mantığı servislere geri ver, orkestratör sadece akışı yönetsin |
| **Chatty Services** | Bir istek 10 senkron çağrı | Aggregation, read model, async |
| **Mimari Kargo Kültü** | "Clean Architecture şablonu indirdik" ama neden bilinmiyor | Problemden başla, mimariyi problem seçer |
| **Her şeye interface** | Tek implementasyonlu yüzlerce interface | Sadece gerçek sınırlarda soyutla |

## 6. Son Söz

- Mimari, **değişimin maliyetini** yönetmekle ilgilidir.
- Her desen bir **ödünleşimdir (trade-off)**: bir problemi çözerken başka bir maliyet getirir.
- **Problemden başlayın, desenden değil.** "Hangi problemi yaşıyorum?" sorusunun cevabı yoksa, o desene ihtiyacınız yok.
- Basit başlayın, ihtiyaç doğdukça evrimleştirin; ama evrimi **mümkün kılacak sınırları** (modüller, bağımlılık yönü) baştan koruyun.

## Kaynaklar

- Eric Evans — *Domain-Driven Design* (2003)
- Vaughn Vernon — *Implementing Domain-Driven Design* (2013)
- Robert C. Martin — *Clean Architecture* (2017)
- Chris Richardson — *Microservices Patterns* (2018), microservices.io
- Sam Newman — *Building Microservices*, 2. baskı (2021); *Monolith to Microservices* (2019)
- Martin Fowler — martinfowler.com (CQRS, Event Sourcing, Strangler Fig, Microservices)
- Jimmy Bogard — "Vertical Slice Architecture" (jimmybogard.com)
- Jeffrey Palermo — "The Onion Architecture" (2008)
- Alistair Cockburn — "Hexagonal Architecture" (2005)
- Microsoft — *.NET Microservices: Architecture for Containerized .NET Applications* (eShopOnContainers / eShop)
- Microsoft Learn — Azure Architecture Center: Cloud Design Patterns
- Garcia-Molina & Salem — "Sagas" (1987)
