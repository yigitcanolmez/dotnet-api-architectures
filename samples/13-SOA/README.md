# 13 — Service-Oriented Architecture (SOA)

İlgili doküman: [Service-Oriented Architecture (SOA)](../../02-dagitik-mimariler/01-soa.md)

**CoreWCF** ile yayınlanan iki SOAP servisi (Customer = *Entity Service*, CreditScoring = *Task Service*) ve
bunları birleştirip bir iş süreci ("kredi başvurusu") oluşturan REST tabanlı bir **Orchestration Service**.
Sözleşmeler WSDL/XSD ile yayınlanır; tüketici sağlayıcıyı yalnızca bu sözleşme üzerinden tanır.

## Bu örnekte ne görüyorsunuz

- **Standartlaştırılmış sözleşme**: `[ServiceContract]`/`[DataContract]` → WSDL + XSD (`?wsdl`), versiyonlu XML namespace'leri.
- **SOAP 1.1 / BasicHttpBinding**: zarf (envelope), `SOAPAction` başlığı, tipli **SOAP Fault** (`FaultException<ServiceFault>`).
- **Servis türleri**: `ICustomerService` (Entity), `ICreditScoringService` (Task/Business), `LoanApprovalOrchestrator` (Orchestration).
- **Birleştirilebilirlik (composability)**: orchestrator, müşteri servisinin çıktısını skor servisine girdi yapıp sonuca süreç kuralı uygular.
- **Protokol köprüsü**: dışarıya REST/JSON, içeride SOAP/XML (`System.ServiceModel.Http 10.1.0` istemcisi).
- **ESB'nin olmadığı** yer ve bunun neden bilinçli olduğu (aşağıda diyagram).

## Klasör ağacı

```
13-SOA/
├── Soa.slnx
└── src/
    ├── Soa.CreditScoringService/              # :5400 — servis sağlayıcı (CoreWCF)
    │   ├── Program.cs                         # AddServiceModelServices, UseServiceModel, HttpGetEnabled (WSDL)
    │   ├── Contracts/
    │   │   ├── ICreditScoringService.cs       # [ServiceContract] + CreditScoreRequest/Response [DataContract]
    │   │   ├── ICustomerService.cs            # [ServiceContract] + GetCustomerRequest/CustomerResponse
    │   │   ├── ServiceFault.cs                # SOAP Fault detayı ([FaultContract])
    │   │   └── Namespaces.cs                  # versiyonlu sözleşme namespace'leri
    │   └── Services/
    │       ├── CreditScoringService.cs        # skor algoritması (gizli — soyutlama), PerCall (durumsuz)
    │       └── CustomerService.cs             # müşteri kayıtları (bellek içi)
    └── Soa.LoanOrchestrator.Api/              # :5401 — tüketici + Orchestration Service (REST)
        ├── Program.cs                         # POST/GET /loan-applications, SOAP Fault → ProblemDetails eşlemesi
        ├── ServiceReferences/                 # ★ svcutil çıktısına eşdeğer el yazımı proxy'ler (servise referans YOK)
        │   ├── CreditScoringContracts.cs
        │   ├── CustomerContracts.cs
        │   ├── ServiceFault.cs
        │   └── SoaChannels.cs                 # ChannelFactory'ler (singleton) + kanal aç/kapat/abort
        └── Orchestration/
            ├── LoanApprovalOrchestrator.cs    # süreç: GetCustomer → GetScore → karar
            ├── LoanDecisionPolicy.cs          # süreç kuralı (risk + taksit/gelir oranı)
            └── LoanApplication.cs
```

## Proje referans grafiği

```
 REST/JSON istemci
        │  POST /loan-applications
        ▼
┌──────────────────────────┐   SOAP 1.1 / HTTP (XML)   ┌──────────────────────────────┐
│ Soa.LoanOrchestrator.Api │ ────────────────────────► │ Soa.CreditScoringService     │
│  ServiceReferences/  ◄───┼──── WSDL/XSD (sözleşme) ──┤  /CustomerService.svc        │
└──────────────────────────┘                           │  /CreditScoringService.svc   │
                                                       └──────────────────────────────┘
ProjectReference: YOK
```

**Neden ProjectReference yok?** SOA'da tüketici ile sağlayıcı farklı platformlarda (Java, COBOL, .NET) olabilir;
ortak bir DLL paylaşmak mümkün değildir. Ortak olan tek şey **sözleşmedir (WSDL/XSD)**. Tüketici, sözleşmeden
kendi proxy'sini üretir (`ServiceReferences/`). Sağlayıcı iç implementasyonunu değiştirebilir; sözleşme (namespace,
eleman adları, sıralama) aynı kaldıkça tüketiciler etkilenmez (gevşek bağlılık).

## Bir isteğin yolculuğu: `POST /loan-applications`

> Bu örnekte sipariş yok; şartnamedeki `POST /orders/{id}/cancel` yerine örneğin merkezindeki süreç izleniyor.

1. `Soa.LoanOrchestrator.Api/Program.cs`: JSON gövde doğrulanır (`ValidationProblem` → 400).
2. `Orchestration/LoanApprovalOrchestrator.cs → ProcessAsync`:
   1. `ServiceReferences/SoaChannels.cs` bir kanal açar, `ICustomerService.GetCustomerAsync` → SOAP zarfı `POST /CustomerService.svc`.
   2. `Soa.CreditScoringService/Services/CustomerService.cs → GetCustomer`: müşteri yoksa `FaultException<ServiceFault>("CUSTOMER_NOT_FOUND")`.
   3. Müşterinin `NationalId` + `MonthlyIncome` değeriyle `ICreditScoringService.GetScoreAsync` → `POST /CreditScoringService.svc`.
   4. `Services/CreditScoringService.cs → GetScore`: skor + risk (Low/Medium/High).
   5. `Orchestration/LoanDecisionPolicy.cs → Decide`: Low → taksit ≤ gelirin %50'si, Medium → ≤ %30, High → ret.
3. `Program.cs`: `201 Created` + başvuru; SOAP Fault → 404/422, bağlantı hatası → 503, timeout → 504 (`ProblemDetails`).

## Çalıştırma

```bash
cd samples/13-SOA
dotnet build
dotnet run --project src/Soa.CreditScoringService     # terminal 1 (:5400)
dotnet run --project src/Soa.LoanOrchestrator.Api     # terminal 2 (:5401)
```

Test projesi yok; doğrulama aşağıdaki `curl` komutlarıyla yapılır. Veri bellek içidir (SQLite gerekmedi).

```bash
# WSDL — sözleşme
curl "http://localhost:5400/CreditScoringService.svc?wsdl"
curl "http://localhost:5400/CreditScoringService.svc?xsd=xsd0"      # veri sözleşmeleri (XSD)

# Orchestration (REST)
curl -s -X POST http://localhost:5401/loan-applications -H 'Content-Type: application/json' \
  -d '{"customerId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","amount":120000,"termMonths":12}'
# → {"customerName":"Ahmet Yılmaz","creditScore":718,"riskLevel":"Low","decision":"Approved",...}
curl -s -X POST http://localhost:5401/loan-applications -H 'Content-Type: application/json' \
  -d '{"customerId":"cccccccc-cccc-cccc-cccc-cccccccccccc","amount":10000,"termMonths":12}'
# → riskLevel "High", decision "Rejected"
curl -s -X POST http://localhost:5401/loan-applications -H 'Content-Type: application/json' \
  -d '{"customerId":"dddddddd-dddd-dddd-dddd-dddddddddddd","amount":10000,"termMonths":12}'
# → 404 ProblemDetails, title "SOAP Fault: CUSTOMER_NOT_FOUND"
```

### SOAP zarfı örneği (ham SOAP isteği)

```bash
curl -s -X POST http://localhost:5400/CreditScoringService.svc \
  -H 'Content-Type: text/xml; charset=utf-8' \
  -H 'SOAPAction: "http://bank.example.com/credit/2026/01/ICreditScoringService/GetScore"' \
  -d '<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
        <s:Body>
          <GetScore xmlns="http://bank.example.com/credit/2026/01">
            <request>
              <NationalId>12345678901</NationalId>
              <MonthlyIncome>45000</MonthlyIncome>
            </request>
          </GetScore>
        </s:Body>
      </s:Envelope>'
```

Yanıt:

```xml
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <s:Body>
    <GetScoreResponse xmlns="http://bank.example.com/credit/2026/01">
      <GetScoreResult xmlns:i="http://www.w3.org/2001/XMLSchema-instance">
        <Score>718</Score><RiskLevel>Low</RiskLevel>
      </GetScoreResult>
    </GetScoreResponse>
  </s:Body>
</s:Envelope>
```

`NationalId`'yi `123` yaparsanız HTTP 500 ve `<s:Fault>` içinde tipli detay döner:
`<detail><ServiceFault><Code>INVALID_NATIONAL_ID</Code>...</ServiceFault></detail>`.
Not: `DataMember(Order=…)` ile eleman sırası sözleşmenin parçasıdır — `MonthlyIncome`'ı `NationalId`'den önce gönderirseniz servis isteği reddeder.

### WSDL'in rolü ve `dotnet-svcutil` ile proxy üretme

WSDL; operasyonları (`GetScore`), mesajları, veri tiplerini (XSD), binding'i (SOAP 1.1/HTTP) ve adresi tek
makine-okunur belgede tanımlar. Java, .NET, Python... hangi platform olursa olsun tüketici bu belgeden istemci kodu üretir.
Bu örnekteki `ServiceReferences/` el ile yazıldı (okunabilir olsun diye); gerçek projede şöyle üretilir:

```bash
dotnet tool install --global dotnet-svcutil
cd src/Soa.LoanOrchestrator.Api
dotnet-svcutil http://localhost:5400/CreditScoringService.svc?wsdl -n "*,Soa.LoanOrchestrator.Api.Generated.Credit" -o CreditScoringReference.cs
dotnet-svcutil http://localhost:5400/CustomerService.svc?wsdl      -n "*,Soa.LoanOrchestrator.Api.Generated.Customer" -o CustomerReference.cs
```

Üretilen `CreditScoringServiceClient : ClientBase<ICreditScoringService>` sınıfı, `SoaChannels`'ın yaptığı işi
(ChannelFactory + kanal yönetimi) hazır olarak sunar.

## SOA prensipleri bu örnekte nerede?

| Prensip (Thomas Erl) | Nerede |
|---|---|
| Standartlaştırılmış sözleşme | `Contracts/*.cs` → `?wsdl`, `?xsd=xsd0`; `Namespaces.cs` (versiyonlu) |
| Gevşek bağlılık | Projeler arası referans yok; tüketici `ServiceReferences/` ile yalnızca sözleşmeye bağlı |
| Soyutlama | Skor algoritması `CreditScoringService.cs` içinde gizli; WSDL'de yalnızca giriş/çıkış var |
| Yeniden kullanılabilirlik | `GetScore` herhangi bir kanal (portal, mobil, çağrı merkezi) tarafından aynen çağrılabilir |
| Otonomi | Servis kendi verisine ve çalışma zamanına sahip (ayrı process, ayrı port) |
| Durumsuzluk | `InstanceContextMode.PerCall`, oturum yok |
| Keşfedilebilirlik | `ServiceMetadataBehavior.HttpGetEnabled` (UDDI'nin minik karşılığı) |
| Birleştirilebilirlik | `LoanApprovalOrchestrator`: Customer + CreditScoring → kredi süreci |

## Mikroservislerden farkı (bu örnek üzerinden)

- Buradaki servisler **kurumsal yetenek** (kredi skoru) etrafında, "herkes yeniden kullansın" diye tasarlandı; mikroservis bir ürünün **bounded context**'i etrafında, bağımsız deploy için tasarlanır.
- İletişim **SOAP/WSDL** (ağır, sıkı şemalı); mikroservislerde REST/gRPC/mesajlaşma.
- Süreç mantığı merkezi bir **orchestrator**'da (gerçekte ESB/BPEL'de); mikroservislerde "aptal boru, akıllı uçlar" — mantık servislerde, sıklıkla koreografi.
- Gerçek SOA'da Customer servisi muhtemelen **paylaşılan kurumsal DB**'yi okurdu; mikroservislerde servis başına DB.

## ESB burada neden yok, gerçek dünyada nerede durur?

Bu örnekte orchestrator servisleri **doğrudan** çağırıyor; routing, dönüşüm ve süreç kodda. Bir ESB ürünü
(BizTalk, MuleSoft, IBM IIB) kurmak bir öğretim örneği için ağır olurdu ve kavram, "orchestration servisi" ile
küçük ölçekte gösterilebiliyor. Gerçek bir kurumda tablo şöyledir:

```
 Web Portal   Mobil   Çağrı Merkezi   Partner B2B          ← Tüketiciler
      └─────────┴──────────┬──────────────┘
                           ▼
 ┌──────────────────────────────────────────────────────────┐
 │                 ENTERPRISE SERVICE BUS                    │
 │  Routing · XSLT dönüşüm · Protokol aracılığı (REST⇄SOAP⇄MQ)│
 │  Orchestration (BPEL) ◄── bu örnekte: LoanApprovalOrchestrator
 │  Güvenlik (WS-Security) · İzleme                          │
 └───────┬───────────────┬────────────────┬─────────────────┘
         ▼               ▼                ▼
   CustomerService  CreditScoring    Mainframe Adapter       ← Servisler
   (.NET/CoreWCF)   (bu örnekte      (COBOL/CICS)
                     aynı process)
         └───────────────┴──── Paylaşılan kurumsal DB ───────┘
```

Bu örnekte ESB'nin üstlendiği işler kodda: protokol aracılığı (`Program.cs`: REST → SOAP), dönüşüm
(`CustomerResponse` → `CreditScoreRequest`), orchestration (`LoanApprovalOrchestrator`). ESB'ye taşındıklarında
merkezi bir ekip tarafından yönetilir — dokümandaki "God ESB" ve darboğaz eleştirisinin kaynağı da budur.

## Deneyin

1. `CreditScoreRequest`'e opsiyonel `[DataMember(Order = 3)] int? ExistingLoanCount` ekleyin; orchestrator'ı değiştirmeden çalışmaya devam ettiğini görün (geriye uyumlu sözleşme evrimi). Sonra bir alanı yeniden adlandırıp sözleşmeyi kırın.
2. `dotnet-svcutil` ile proxy üretip `ServiceReferences/` yerine üretilmiş `CreditScoringServiceClient`'ı kullanın; farkları karşılaştırın.
3. `ICreditScoringService`'e ikinci bir endpoint ekleyin: aynı servisi `WSHttpBinding` (SOAP 1.2 + WS-Addressing) ile yayınlayıp ham zarfın nasıl değiştiğini (`<a:Action>` başlığı) inceleyin.

## Artı / eksi gözlemleri

- ✅ Sözleşme (WSDL/XSD) platformdan bağımsız ve makine-okunur; tipli fault'lar hata sözleşmesini de standartlaştırıyor.
- ✅ Skor servisi gerçekten yeniden kullanılabilir; yeni bir kanal yalnızca WSDL'i alıp entegre olur.
- ❌ XML zarfı, namespace'ler, eleman sırası, `SOAPAction`... basit bir çağrı için bile ciddi tören.
- ❌ Orchestrator tüm süreci bildiği için büyüdükçe "God orchestrator/ESB"ye dönüşme eğiliminde.
- ❌ Senkron zincir: CreditScoringService düştüğünde başvuru süreci de düşüyor (503) — bkz. [12-Resilience](../12-Resilience/README.md).
