# 11. Service-Oriented Architecture (SOA)

> 💻 **Çalışan örnek proje:** [`samples/13-SOA`](../samples/13-SOA/)

> 1990'ların sonu – 2000'ler; IBM, Oracle, Microsoft (WCF), OASIS standartları

## Neden Ortaya Çıktı?

Büyük kurumlarda (banka, telekom, sigorta) onlarca **birbirinden kopuk sistem** vardı: mainframe COBOL, ERP (SAP), CRM, Java uygulamaları, .NET uygulamaları. Sorunlar:

- **Bilgi silolar**: Müşteri bilgisi 7 farklı sistemde, 7 farklı formatta.
- **Noktadan noktaya entegrasyon**: N sistem için N×(N-1) entegrasyon → "spagetti entegrasyon".
- Aynı iş yeteneği (ör. "kredi skoru hesapla") her sistemde **yeniden yazılıyordu**.
- Farklı platformlar birbirleriyle konuşamıyordu (Java ↔ .NET ↔ COBOL).

## Çözdüğü Problem

> "Kurumsal iş yeteneklerini, platformdan bağımsız, standart sözleşmeli, **yeniden kullanılabilir servisler** olarak sun ve bunları birleştirerek (compose) iş süreçleri oluştur."

## Yapı

```
┌────────────┐ ┌────────────┐ ┌────────────┐ ┌────────────┐
│ Web Portal │ │ Mobile     │ │ Call Center│ │ Partner B2B│     Tüketiciler
└─────┬──────┘ └─────┬──────┘ └─────┬──────┘ └─────┬──────┘
      └──────────────┴──────┬───────┴──────────────┘
                            ▼
   ┌─────────────────────────────────────────────────────────┐
   │        ENTERPRISE SERVICE BUS (ESB)                     │
   │  Routing · Transformation (XSLT) · Protocol mediation   │
   │  Orchestration (BPEL) · Security · Monitoring           │
   └──┬─────────────┬────────────────┬───────────────┬───────┘
      ▼             ▼                ▼               ▼
┌───────────┐ ┌───────────┐  ┌──────────────┐ ┌─────────────┐
│ Customer  │ │ Credit    │  │ Billing      │ │ Legacy      │   Servisler
│ Service   │ │ Scoring   │  │ Service (SAP)│ │ Mainframe   │
│ (.NET WCF)│ │ (Java)    │  │              │ │ (Adapter)   │
└─────┬─────┘ └─────┬─────┘  └──────┬───────┘ └──────┬──────┘
      └─────────────┴───── PAYLAŞILAN KURUMSAL VERİTABANI ───┘  (sıklıkla)
```

## Temel Prensipler (Thomas Erl)

1. **Standartlaştırılmış Sözleşme** (WSDL, XSD)
2. **Gevşek Bağlılık**
3. **Soyutlama** (iç detay gizli)
4. **Yeniden Kullanılabilirlik** — SOA'nın ana hedefi
5. **Otonomi**
6. **Durumsuzluk (Statelessness)**
7. **Keşfedilebilirlik** (UDDI registry)
8. **Birleştirilebilirlik (Composability)**

## Servis Türleri

| Tür | Örnek |
|-----|-------|
| **Entity Service** | CustomerService, ProductService (veri odaklı) |
| **Task/Business Service** | LoanApprovalService (süreç) |
| **Utility Service** | Logging, Notification, Audit |
| **Orchestration Service** | BPEL ile birden çok servisi yöneten süreç |

## Teknolojiler

- **SOAP + WSDL + XSD**, WS-* standartları (WS-Security, WS-ReliableMessaging, WS-AtomicTransaction)
- **.NET'te**: WCF (Windows Communication Foundation), BizTalk Server
- **ESB**: MuleSoft, IBM Integration Bus, Oracle Service Bus, BizTalk, WSO2
- **.NET (modern)**: CoreWCF (WCF'nin .NET Core portu), gRPC

### .NET — CoreWCF örneği

```csharp
[ServiceContract(Namespace = "http://bank.com/credit")]
public interface ICreditScoringService
{
    [OperationContract]
    CreditScoreResponse GetScore(CreditScoreRequest request);
}

[DataContract]
public class CreditScoreRequest { [DataMember] public string NationalId { get; set; } = ""; }

public class CreditScoringService : ICreditScoringService
{
    public CreditScoreResponse GetScore(CreditScoreRequest r) => new() { Score = 742 };
}

// Program.cs
builder.Services.AddServiceModelServices();
app.UseServiceModel(sb =>
{
    sb.AddService<CreditScoringService>();
    sb.AddServiceEndpoint<CreditScoringService, ICreditScoringService>(new BasicHttpBinding(), "/CreditScoring.svc");
});
```

## SOA'nın Sorunları (Mikroservislerin Doğuş Sebebi)

- ❌ **ESB tek hata ve darboğaz noktası** oldu; içine iş mantığı (routing kuralları, dönüşümler) gömüldü → "God ESB".
- ❌ ESB'yi yöneten **merkezi entegrasyon ekibi** tüm değişikliklerde darboğaz.
- ❌ **Paylaşılan veritabanı** → servisler aslında bağımsız değildi.
- ❌ "Yeniden kullanım" hedefi aşırı genel, şişkin servisler doğurdu (`CustomerService` 300 operasyon).
- ❌ SOAP/WS-* ağır, karmaşık, XML yığını.
- ❌ Pahalı ticari ürünler, vendor lock-in.
- ❌ Bağımsız deploy genellikle mümkün değildi.

## SOA vs Mikroservisler

| | SOA | Mikroservisler |
|---|---|---|
| Kapsam | Kurumsal (enterprise) entegrasyon | Tek uygulama/ürün |
| Hedef | **Yeniden kullanım** | **Bağımsız değişim ve deploy** |
| İletişim | Akıllı boru (ESB), aptal uçlar | **Aptal boru, akıllı uçlar** |
| Veri | Sıklıkla paylaşılan DB | Servis başına DB |
| Protokol | SOAP, WS-* | REST, gRPC, mesajlaşma |
| Yönetişim | Merkezi | Merkezi olmayan |
| Servis boyutu | Büyük, genel | Küçük, bounded context |

> "Mikroservisler = SOA'nın doğru yapılmış hali" sözü yaygındır, ama hedefleri farklıdır.

## Günümüzde SOA Ne Zaman Anlamlı?

- Büyük kurumda **heterojen legacy sistemlerin** entegrasyonu
- Banka/telekom gibi SOAP tabanlı partner entegrasyonları
- Modern karşılığı: **API-led connectivity** (MuleSoft), **Azure Integration Services** (APIM + Logic Apps + Service Bus)
