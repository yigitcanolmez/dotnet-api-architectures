# 05 — Vertical Slice Architecture (VSA) + REPR

📖 İlgili dokümanlar: [Vertical Slice Architecture](../../01-uygulama-ici-mimariler/05-vertical-slice-architecture.md) · [Minimal API ve REPR](../../01-uygulama-ici-mimariler/10-minimal-api-repr.md)

> **VSA bir organizasyon prensibidir, kütüphane değil.**
> Bu örnekte MediatR, Carter, FastEndpoints… hiçbiri yok. Her dilimin `Handler`'ı sıradan bir sınıftır;
> DI'a kaydedilir ve Minimal API endpoint lambda'sına **doğrudan parametre olarak enjekte edilir**.
> MediatR yalnızca bir in-process mesajlaşma aracıdır; VSA'nın özü "birlikte değişen kod birlikte durur" fikridir.

## Bu örnekte ne görüyorsunuz?

- **Özelliğe göre (dikey) bölünmüş kod**: `Features/Orders/PlaceOrder.cs` tek dosyada Request + Response + Validator + Handler + Endpoint (REPR) içerir.
- **Her dilim kendi yaklaşımını seçer**: okuma dilimleri (`GetOrder`, `ListOrders`, `ListProducts`) domain'i hiç kullanmadan `AsNoTracking()` + DTO projeksiyonu yapar; yazma dilimleri (`PlaceOrder`, `ShipOrder`, `CancelOrder`) zengin domain metotlarını çağırır (**VSA + DDD hibrit**).
- **Assembly taraması**: `Common/EndpointExtensions.cs` reflection ile tüm `IEndpoint` ve `IHandler` sınıflarını bulur → `Program.cs` hiçbir özelliği tanımaz.
- **Open/Closed**: `Features/Products/RestockProduct.cs` sonradan eklendi; **başka hiçbir dosya değişmedi**.
- **MediatR pipeline behavior yerine endpoint filter**: `Common/ValidationFilter<T>` FluentValidation'ı handler'dan önce çalıştırır.
- **Slice bazlı integration testler**: `WebApplicationFactory` ile her dilim HTTP'den veritabanına kadar uçtan uca test edilir.

## Klasör yapısı

```
05-VerticalSlice/
├── VerticalSlice.slnx
├── src/VerticalSlice.Api/                  ← TEK proje
│   ├── Program.cs                          ← composition root; dilimleri tek tek bilmez (port 5005)
│   ├── VerticalSlice.Api.http              ← tüm endpoint'ler için örnek istekler
│   ├── Features/                           ← ★ mimarinin kalbi: her dosya = bir istek = bir dilim
│   │   ├── Orders/
│   │   │   ├── PlaceOrder.cs               ← POST /orders          (Request/Response/Validator/Handler/Endpoint)
│   │   │   ├── GetOrder.cs                 ← GET  /orders/{id}     (okuma: AsNoTracking projeksiyon)
│   │   │   ├── ListOrders.cs               ← GET  /orders?customerId=
│   │   │   ├── ShipOrder.cs                ← POST /orders/{id}/ship
│   │   │   └── CancelOrder.cs              ← POST /orders/{id}/cancel
│   │   └── Products/
│   │       ├── ListProducts.cs             ← GET  /products
│   │       └── RestockProduct.cs           ← POST /products/{id}/restock (sonradan eklenen dilim)
│   ├── Domain/                             ← sadece gerektiği kadar zengin model
│   │   ├── Order.cs                        ← Place/Ship/Cancel kuralları (invariant'lar)
│   │   └── Product.cs                      ← stok düş / stok ekle kuralları
│   ├── Infrastructure/
│   │   ├── AppDbContext.cs                 ← EF Core (repository soyutlaması YOK)
│   │   └── Seed.cs                         ← EnsureCreated + 3 ürün
│   └── Common/                             ← gerçekten ortak olanlar
│       ├── IEndpoint.cs                    ← IEndpoint + IHandler marker arayüzleri
│       ├── EndpointExtensions.cs           ← reflection ile AddSlices / MapEndpoints
│       ├── ValidationFilter.cs             ← ValidationFilter<T> (IEndpointFilter)
│       └── Result.cs                       ← Result / Result<T> / Error → ProblemDetails
└── tests/VerticalSlice.Tests/
    ├── VerticalSliceApiFactory.cs          ← WebApplicationFactory<Program> + test başına ayrı SQLite dosyası
    └── Features/                           ← testler de dilimlerle aynı şekilde organize
        ├── Orders/{PlaceOrder,GetOrder,ShipOrder,CancelOrder}Tests.cs
        └── Products/RestockProductTests.cs
```

## Referans grafiği

Proje seviyesinde tek ok vardır:

```
VerticalSlice.Tests ──► VerticalSlice.Api
```

Proje **içindeki** bağımlılık yönü (klasörler arası):

```
                ┌──────────────► Common (IEndpoint, Result, ValidationFilter)
                │                   ▲
 Features/* ────┼──► Domain ────────┘
   (dilimler)   │       ▲
                └──► Infrastructure (AppDbContext) ──► Domain

 Features/Orders/X.cs  ──✗──►  Features/Orders/Y.cs   (dilimler birbirini çağırmaz!)
```

**Neden bu yönde?** VSA'da katman projeleri yoktur; çoğu zaman **tek proje** yeterlidir (dokümandaki öneri). Bağımlılık kuralı "katmanlar arası" değil **"dilimler arası"** tanımlanır: bir dilim diğerinin handler'ını çağırmaz, diğerinin DTO'sunu kullanmaz. Ortak ihtiyaç doğduğunda (ör. stok kuralı üç dilimde gerekti) kod `Domain/` veya `Common/` altına **sonradan** çekilir. `Domain` hiçbir şeye bağımlı değildir (sadece `Common/Result`), bu yüzden kurallar EF/HTTP'den bağımsız kalır.

## Bir isteğin yolculuğu: `POST /orders/{id}/cancel`

1. **`Program.cs`** → uygulama başlarken `app.MapEndpoints()` çağrılır.
2. **`Common/EndpointExtensions.cs`** → reflection ile bulunan `CancelOrder.Endpoint` route'u (`/orders/{id:guid}/cancel`) kaydetmiştir; `CancelOrder.Handler` da `AddSlices` ile scoped olarak DI'a eklenmiştir.
3. **`Features/Orders/CancelOrder.cs` → `Endpoint`** → Minimal API `Guid id` ve `Handler`'ı (DI'dan, MediatR'sız) lambda'ya enjekte eder.
4. **`Features/Orders/CancelOrder.cs` → `Handler.HandleAsync`** → `AppDbContext` ile siparişi (kalemleriyle) yükler; yoksa `Error.NotFound`.
5. **`Domain/Order.cs` → `Cancel()`** → kural: `Shipped` → `Error.Conflict`, zaten `Cancelled` → `Error.Conflict`; aksi halde durum `Cancelled`.
6. **`Domain/Product.cs` → `Restock()`** → iptal edilen kalemlerin stoğu iade edilir.
7. **`Infrastructure/AppDbContext.cs`** → `SaveChangesAsync` tek transaction'da sipariş + ürünleri yazar.
8. **`Common/Result.cs` → `Error.ToProblem()`** → başarı: `204 No Content`; hata: `409`/`404` `ProblemDetails`.

Toplam: **1 özellik dosyası** + paylaşılan domain. Clean Architecture'daki aynı akış Controller → Command → Handler → IRepository → Repository → Entity olarak 4 projeye yayılır.

## Karşılaştırma: "Sipariş iptaline **iptal sebebi** alanı ekle" kaç dosyaya dokunur?

| Adım | Vertical Slice (bu örnek) | Clean Architecture ([04-CleanArchitecture](../04-CleanArchitecture/)) |
|---|---|---|
| İstek gövdesi (`Reason`) | `Features/Orders/CancelOrder.cs` | `Api/.../OrdersEndpoints` (request DTO) |
| Validasyon | aynı dosya (`Validator`) | `Application/.../CancelOrderCommandValidator.cs` |
| Komut / handler | aynı dosya (`Handler`) | `Application/.../CancelOrderCommand.cs` + `CancelOrderCommandHandler.cs` |
| Domain kuralı + alan | `Domain/Order.cs` (`Cancel(reason)`) | `Domain/.../Order.cs` |
| EF mapping | (gerekirse) `Infrastructure/AppDbContext.cs` | `Infrastructure/.../OrderConfiguration.cs` |
| Okuma DTO'su | (isterseniz) `GetOrder.cs` | `Application/.../OrderDto.cs` + mapping |
| **Toplam** | **1–2 dosya, 1 proje** | **~6 dosya, 4 proje** |

## Çalıştırma

```bash
cd samples/05-VerticalSlice
dotnet build
dotnet test
dotnet run --project src/VerticalSlice.Api      # http://localhost:5005
```

> Not: Öğretim amaçlı sadelik için **migration yok**; başlangıçta `EnsureCreated()` + seed çalışır (`verticalslice.db`). Şemayı değiştirirseniz `.db` dosyasını silin.

```bash
B=http://localhost:5005
curl -s $B/products

# sipariş ver → {"id":"..."}
ID=$(curl -s -X POST $B/orders -H 'Content-Type: application/json' \
  -d '{"customerId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","items":[{"productId":"11111111-1111-1111-1111-111111111111","quantity":2}]}' \
  | sed 's/.*"id":"\([^"]*\)".*/\1/')

curl -s $B/orders/$ID
curl -s -i -X POST $B/orders/$ID/ship      # 204
curl -s -i -X POST $B/orders/$ID/cancel    # 409 (kargolanmış sipariş iptal edilemez)
curl -s "$B/orders?customerId=aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"

# sonradan eklenen dilim
curl -s -X POST $B/products/33333333-3333-3333-3333-333333333333/restock \
  -H 'Content-Type: application/json' -d '{"quantity":5}'
```

Tüm istekler için: `src/VerticalSlice.Api/VerticalSlice.Api.http`.

### Neden integration test?

VSA'da bir dilimin "birimi" endpoint + validator + handler + domain + EF sorgusunun **bütünüdür**. Handler'ı mock'larla izole etmek (ör. `DbContext`'i mock'lamak) dilimin asıl davranışını — doğru SQL projeksiyonu, validation filter'ın devreye girmesi, doğru HTTP kodu — test etmez. Bu yüzden VSA'da en değerli test türü **slice bazlı integration test**tir: `WebApplicationFactory<Program>` gerçek uygulamayı bellekte ayağa kaldırır, test HTTP isteği atar ve sonucu doğrular. Her test sınıfı kendi SQLite dosyasını kullanır. Saf domain kuralları karmaşıklaşırsa (`Domain/Order.cs`) onlar için ayrıca hızlı unit testler eklenebilir.

## Deneyin

1. **İptal sebebi**: `CancelOrder.cs`'e `Request(string Reason)` + `Validator` (boş olamaz, max 200) ekleyin, `Order.Cancel(reason)` ile saklayın. Kaç dosyaya dokunduğunuzu sayın ve yukarıdaki tabloyla karşılaştırın (`.db` dosyasını silmeyi unutmayın).
2. **Yeni dilim**: `Features/Products/UpdatePrice.cs` (`PUT /products/{id}/price`) ekleyin. `Program.cs`'e dokunmadan çalıştığını görün.
3. **Farklı teknik yaklaşım**: `ListOrders.cs` handler'ını EF yerine ham SQL ile (`db.Database.SqlQuery<T>(...)`) yazın — diğer dilimler bundan etkilenmez.

## Bu örnekteki gözlemler

**Artılar**
- ✅ Bir özelliği anlamak/değiştirmek için genellikle tek dosya açılıyor.
- ✅ Yeni özellik eklemek mevcut kodu bozmuyor (`RestockProduct` örneği).
- ✅ Okuma dilimleri domain/mapping yükü taşımıyor; yazma dilimleri domain kurallarını yeniden kullanıyor.
- ✅ MediatR olmadan da aynı fayda: daha az soyutlama, "Go to Definition" doğrudan handler'a gidiyor.

**Eksiler**
- ❌ `GetOrder.Response` ve `ListOrders.Response` benzer — bilinçli tekrar; kontrolsüz kalırsa dağınıklığa döner.
- ❌ "Ne zaman `Domain/`'e çekmeli?" kararı ekip disiplini istiyor (stok kuralı bunun örneği).
- ❌ Katman yokluğundan dolayı NetArchTest tarzı mimari kurallar daha az anlamlı; dilimler arası bağımlılığı code review korumalı.
- ❌ Reflection ile tarama Native AOT ile uyumsuz (gerekirse source generator / elle kayıt).
