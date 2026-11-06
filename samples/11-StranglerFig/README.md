# 11 — Strangler Fig + Anti-Corruption Layer

İlgili dokümanlar:
- [Strangler Fig Deseni](../../03-dagitik-desenler/04-strangler-fig.md)
- [Anti-Corruption Layer (Veri ve Entegrasyon Desenleri §20.3)](../../03-dagitik-desenler/06-veri-ve-entegrasyon-desenleri.md)

Bir "legacy" sipariş sisteminin önüne **YARP** ile bir proxy koyup, **Orders** özelliğini temiz bir
**Modern.Api**'ye taşıyoruz. Ürünler henüz taşınmadığı için modern sistem ürünleri legacy'den bir
**Anti-Corruption Layer (ACL)** üzerinden okuyor.

## Bu örnekte ne görüyorsunuz

- **Yönlendirme katmanı (facade/proxy)**: İstemciler yalnızca `:5200`'ü bilir; arkada legacy mi modern mi çalıştığı gizlidir.
- **Konfigürasyonla yönetilen route'lar**: `/api/orders/**` → modern (`Order=1`), geri kalan her şey → legacy (catch-all, `Order=1000`). Route tablosu koddan üretilir (`LoadFromMemory`).
- **Feature flag ile anında rollback**: `Strangler:OrdersMigrated` değiştirilince proxy route'u **yeniden başlatmadan** legacy/modern arasında çevirir.
- **`X-Served-By: legacy|modern`** yanıt başlığı: hangi sistemin cevap verdiğini her istekte görürsünüz.
- **Legacy stil**: controller'da ham SQL, `ORD_NO / CUST_ID / STAT_CD` alanları, `"1.500,00"` gibi string tutarlar.
- **ACL**: `LegacyProductTranslator`, legacy'nin `PRD_NM/PRC/STAT_CD` modelini domain'in `Product`/`Money` modeline çevirir; legacy kavramları domain'e sızmaz.

## Klasör ağacı

```
11-StranglerFig/
├── StranglerFig.slnx
├── run.sh                                  # 3 uygulamayı birlikte başlatır (Ctrl+C ile durur)
├── src/
│   ├── Proxy/                              # :5200 — Strangler "boğucu incir"
│   │   ├── Program.cs                      # YARP + flag değişince InMemoryConfigProvider.Update
│   │   ├── StranglerRouteTable.cs          # Route/cluster tablosu KODDAN; X-Served-By transform'u
│   │   ├── StranglerOptions.cs             # OrdersMigrated flag'i + adresler
│   │   └── appsettings.json                # "Strangler": { "OrdersMigrated": true, ... }
│   ├── Legacy.Api/                         # :5201 — "eski sistem" (bilerek kötü)
│   │   ├── Program.cs
│   │   ├── LegacyDb.cs                     # statik DAL, ham SQL şema + seed, "1.500,00" tutar formatı
│   │   └── Controllers/LegacyController.cs # God controller: orders + products + customers
│   └── Modern.Api/                         # :5202 — Orders'ın yeniden yazılmış hali (VSA + rich domain)
│       ├── Domain/                         # Order, OrderLine, Money, Product, IProductCatalog (port)
│       ├── AntiCorruption/
│       │   ├── LegacyProductDto.cs         # legacy'nin dili (internal, sadece burada yaşar)
│       │   └── LegacyProductTranslator.cs  # ★ ACL: HTTP çağrısı + çeviri → IProductCatalog
│       ├── Data/ModernDbContext.cs         # modern sistemin KENDİ DB'si (strangler-modern.db)
│       ├── Features/Orders/*.cs            # PlaceOrder, GetOrder, ListOrders, ShipOrder, CancelOrder
│       └── Features/AclDemo/ListProductsViaAcl.cs  # GET /acl/products: ACL çıktısını görmek için
└── tests/Modern.Api.Tests/                 # ACL çeviri testleri + Order invariant testleri
```

## Proje referans grafiği

```
          İstemci
             │  (yalnızca :5200)
             ▼
        ┌─────────┐   HTTP (YARP)   ┌─────────────┐
        │  Proxy  │ ──────────────► │ Legacy.Api  │◄──────┐
        └─────────┘                 └─────────────┘       │ HTTP GET /api/products
             │ HTTP (YARP)                                │ (ACL üzerinden)
             ▼                                            │
        ┌─────────────┐  Features → Domain ← AntiCorruption
        │ Modern.Api  │──────────────────────────────────┘
        └─────────────┘
Modern.Api.Tests ──(ProjectReference)──► Modern.Api
```

Üç uygulama arasında **hiç ProjectReference yok**; yalnızca HTTP ile konuşurlar. Neden?
- Legacy'ye dokunmadan taşıma yapabilmek Strangler Fig'in temel şartıdır — legacy kodu yeniden derlenmez, referans verilmez.
- Modern sistem legacy'nin tiplerini paylaşmaz; legacy'nin JSON'unu kendi `LegacyProductDto`'su ile okur ve **hemen** çevirir.
- Modern.Api içinde bağımlılık yönü: `Features → Domain` ve `AntiCorruption → Domain`. Domain, `IProductCatalog` portunu tanımlar; ACL bu portu uygular (Hexagonal'daki driven adapter).

## Fazlar

```
Faz 1 (proxy kondu)        Faz 2 (bu örnek, flag=true)        Faz 3 (hedef)
     İstemci                       İstemci                        İstemci
        │                             │                              │
    [Proxy:5200]                 [Proxy:5200]                   [Proxy:5200]
        │                        │          │                        │
   Legacy (100%)            Legacy        Modern                Modern (100%)
                     (/api/products,  (/api/orders/**)          Legacy ⚰️ kapatıldı
                      /api/customers)       │
                             ▲──── ACL ─────┘  (ürünler hâlâ legacy'den)
```

- **Faz 1** = `OrdersMigrated: false` → her şey legacy'ye gider; proxy sadece "araya girmiştir".
- **Faz 2** = `OrdersMigrated: true` → Orders modern'de, kalanlar legacy'de. ACL köprü görevi görür.
- **Faz 3** = Products ve Customers da taşındığında catch-all kaldırılır, `LegacyProductTranslator` yerine modern bir `IProductCatalog` yazılır (domain değişmez) ve legacy kapatılır.

## Bir isteğin yolculuğu: `POST /api/orders/{id}/cancel` (flag = true)

1. İstemci → `http://localhost:5200/api/orders/{id}/cancel`.
2. `src/Proxy/StranglerRouteTable.cs`: `orders` route'u (`/api/orders/{**rest}`, `Order=1`) catch-all'dan önce eşleşir; flag `true` olduğu için `ClusterId = "modern"`.
3. `src/Proxy/Program.cs`: `MapReverseProxy()` isteği `http://localhost:5202`'ye iletir.
4. `src/Modern.Api/Features/Orders/CancelOrder.cs`: `ModernDbContext` ile siparişi yükler, `order.Cancel()` çağırır.
5. `src/Modern.Api/Domain/Order.cs`: `Shipped` ise `DomainException(isConflict: true)` fırlatır.
6. `src/Modern.Api/Program.cs`: `UseExceptionHandler` → `409 ProblemDetails`; başarıda `204`.
7. Proxy yanıta `X-Served-By: modern` ve `X-Strangler-Route: orders` başlıklarını ekler.

Flag `false` olduğunda aynı istek 2. adımda `legacy` cluster'ına gider ve `Legacy.Api/Controllers/LegacyController.cs → UPD_STAT` çalışır (hata gövdesi legacy formatında: `{"ERR_CD":"E409",...}`).

## Çalıştırma

```bash
cd samples/11-StranglerFig
dotnet build
dotnet test                       # ACL + domain testleri
./run.sh                          # Legacy, Modern ve Proxy'yi birlikte başlatır
# veya ayrı terminallerde:
dotnet run --project src/Legacy.Api
dotnet run --project src/Modern.Api
dotnet run --project src/Proxy
```

> SQLite dosyaları (`strangler-legacy.db`, `strangler-modern.db`) ilk açılışta oluşturulur ve seed edilir; migration yok (öğretim amaçlı sadelik).

### `X-Served-By` gözlemi

```bash
curl -si http://localhost:5200/api/products  | grep -i x-served-by   # X-Served-By: legacy
curl -si http://localhost:5200/api/customers | grep -i x-served-by   # X-Served-By: legacy
curl -si http://localhost:5200/api/orders    | grep -i x-served-by   # X-Served-By: modern
curl -s  http://localhost:5200/_strangler                            # anlık route tablosu

# Sipariş oluştur → getir → kargola → iptal dene (409)
ID=$(curl -s -X POST http://localhost:5200/api/orders -H 'Content-Type: application/json' \
  -d '{"customerId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","items":[{"productId":"11111111-1111-1111-1111-111111111111","quantity":2}]}' \
  | sed -E 's/.*"id":"([^"]+)".*/\1/')
curl -s  http://localhost:5200/api/orders/$ID
curl -si -X POST http://localhost:5200/api/orders/$ID/ship   | head -1   # 204
curl -si -X POST http://localhost:5200/api/orders/$ID/cancel | head -1   # 409
```

### ACL'i gözlemleme

```bash
curl -s http://localhost:5201/api/products   # legacy:  {"PRD_NM":"MONITOR","PRC":"6.500,00","STAT_CD":"A"}
curl -s http://localhost:5202/acl/products   # domain:  {"name":"Monitör","price":{"amount":6500.00,"currency":"TRY"},"isSellable":true}
```

### Rollback senaryosu (flag'i kapatıp geri dönme)

Modern sistemde bir hata keşfettiniz; Orders trafiğini **deploy yapmadan** legacy'ye geri verin:

```bash
# 1) Flag'i kapat (proxy dosyayı izler; 1-5 sn içinde route tablosu yeniden yüklenir, proxy log'unda uyarı görünür)
sed -i 's/"OrdersMigrated": true/"OrdersMigrated": false/' src/Proxy/appsettings.json
curl -si http://localhost:5200/api/orders | grep -i x-served-by      # X-Served-By: legacy

# 2) Legacy sözleşmesiyle sipariş (legacy'nin alan adları!)
curl -s -X POST http://localhost:5200/api/orders -H 'Content-Type: application/json' \
  -d '{"CUST_ID":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","KALEMLER":[{"PRD_ID":"11111111-1111-1111-1111-111111111111","ADET":2}]}'

# 3) Sorun giderildi → yeniden modern'e
sed -i 's/"OrdersMigrated": false/"OrdersMigrated": true/' src/Proxy/appsettings.json
```

> ⚠️ **Bilinçli basitleştirmeler** — rollback sırasında iki gerçek dünyaya ait sorunu açıkça görürsünüz:
> 1. **Sözleşme farkı**: legacy `ORD_NO/KALEMLER`, modern `id/items` konuşur. Gerçek bir geçişte dış sözleşme korunur
>    (modern sistem legacy-uyumlu bir "facade" sunar ya da proxy'de dönüşüm yapılır) veya versiyonlanır.
> 2. **Veri senkronizasyonu**: iki sistemin ayrı DB'leri var; modern'de oluşan sipariş legacy'de görünmez (ve tersi).
>    Gerçekte CDC, olay yayını veya çift yazma (dual-write) ile senkronize edilir — dokümandaki "Veri senkronizasyonu" bölümü.
>
> Legacy hata gövdeleri ProblemDetails değildir (`{"ERR_CD","ERR_MSG"}`) — bu da legacy'nin bir parçası.

## ACL neden gerekli?

Modern.Api'nin sipariş verebilmesi için ürün adı/fiyatı/satılabilirliği lazım, ama ürünler hâlâ legacy'de. ACL olmasaydı:
- `PRD_NM`, `PRC: "6.500,00"`, `STAT_CD: "P"` gibi alanlar `Order`'a, endpoint'lere, testlere **sızar**; domain legacy'nin diliyle konuşmaya başlar (= "corrupt").
- Türkçe biçimli string tutar parse'ı, `"A"/"P"` kodlarının anlamı gibi bilgi kod tabanına dağılır.
- Ürünler taşındığında tüm bu yerleri değiştirmek gerekir.

ACL ile: domain yalnızca `IProductCatalog` / `Product` / `Money` bilir. Legacy değişirse ya da ürünler taşınırsa **sadece `AntiCorruption/` klasörü** değişir.
`LegacyProductTranslator.TryTranslate` saf bir fonksiyon olduğu için HTTP olmadan birim test edilir (`tests/Modern.Api.Tests/LegacyProductTranslatorTests.cs`).

## Deneyin

1. **Customers'ı taşıyın**: Modern.Api'ye `GET /api/customers` ekleyin, `StranglerRouteTable`'a `Strangler:CustomersMigrated` flag'li yeni bir route (`Order=2`) koyun ve `X-Served-By` ile gözlemleyin.
2. **Faz 3'e geçin**: Ürünleri Modern'in kendi DB'sine taşıyıp `IProductCatalog`'un yeni bir implementasyonunu yazın; `Program.cs`'te tek satır DI değişikliğiyle ACL'i devreden çıkarın — domain ve feature'lar değişmemeli.
3. **Legacy-uyumlu sözleşme**: Modern.Api'ye `ORD_NO/KALEMLER` formatını kabul eden bir uyumluluk endpoint'i (veya YARP request transform'u) ekleyip rollback'in istemci açısından şeffaf olmasını sağlayın.

## Artı / eksi gözlemleri

- ✅ Legacy'ye **hiç dokunmadan** bir özellik taşındı; geri dönüş bir config satırı.
- ✅ `X-Served-By` + `/_strangler` ile geçişin her an gözlemlenebilir olması.
- ✅ ACL sayesinde modern domain temiz kaldı; legacy kapanınca silinecek kod tek klasörde.
- ❌ Geçiş süresince **üç** uygulama işletiliyor (proxy + legacy + modern) — operasyon maliyeti.
- ❌ İki DB arasında veri senkronizasyonu çözülmedi; sözleşme uyumsuzluğu rollback'i istemciye yansıtıyor.
- ❌ Modern sistem çalışma zamanında legacy'ye bağımlı (ürünler): legacy çökerse sipariş verilemez (503) — "hiç bitmeyen geçiş" riski.
