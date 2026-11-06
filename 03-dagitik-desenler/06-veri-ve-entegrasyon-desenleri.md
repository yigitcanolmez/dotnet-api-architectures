# 20. Veri ve Entegrasyon Desenleri

> 💻 **Çalışan örnek proje:** [`samples/11-StranglerFig`](../samples/11-StranglerFig/) · [`samples/10-Microservices`](../samples/10-Microservices/)

## 20.1 Database per Service

### Neden?
Paylaşılan veritabanı, servisleri **gizlice birbirine bağlar**:
- Bir servis kolon adını değiştirince diğerleri kırılır.
- Şema değişiklikleri tüm ekiplerin koordinasyonunu gerektirir → bağımsız deploy biter.
- Bir servisin ağır sorgusu diğerlerini yavaşlatır.
- Kim hangi veriden sorumlu belirsizleşir.

### Çözüm
> Her servis kendi verisine **tek başına sahiptir**. Başka servisler bu veriye yalnızca servisin API'si veya yayınladığı olaylar üzerinden erişir.

Seçenekler (izolasyon artan sırada): ayrı tablo seti → **ayrı schema** → ayrı DB → ayrı DB sunucusu.
Her servis kendi ihtiyacına göre DB seçebilir (**polyglot persistence**): sipariş → SQL, katalog → Elasticsearch, sepet → Redis.

### Getirdiği Problemler
- Servisler arası transaction → **Saga**
- Servisler arası sorgu/JOIN → **API Composition** veya **CQRS read model**

---

## 20.2 API Composition

### Problem
"Sipariş detayı" ekranı; sipariş (Ordering), ürün adları (Catalog), kargo durumu (Shipping) bilgisi istiyor. JOIN yapılamaz.

### Çözüm
Bir **composer** (gateway, BFF veya ayrı servis) ilgili servisleri **paralel** çağırıp sonuçları bellekte birleştirir.

```csharp
app.MapGet("/order-details/{id}", async (Guid id, OrderingClient o, CatalogClient c, ShippingClient s) =>
{
    var order = await o.GetAsync(id);
    var productsTask = c.GetManyAsync(order.Lines.Select(l => l.ProductId));
    var shipmentTask = s.GetByOrderAsync(id);
    await Task.WhenAll(productsTask, shipmentTask);
    return new OrderDetails(order, productsTask.Result, shipmentTask.Result);
});
```

- ✅ Basit. ❌ Büyük veri setlerinde bellekte JOIN verimsiz; availability düşer (tüm servisler ayakta olmalı).
- Alternatif: **CQRS read model** — ilgili olayları dinleyip önceden birleştirilmiş bir görünüm (view) tutan servis. Hızlı ve dayanıklı, ama eventual consistent.
- GraphQL Federation (**Hot Chocolate Fusion**) de bu problemin çözümüdür.

---

## 20.3 Anti-Corruption Layer (ACL)

### Problem
Yeni, temiz domain modeliniz legacy bir sistemle veya 3. parti API ile (ERP, bankanın SOAP servisi) konuşmalı. Onların modeli (`CUST_NO`, `STAT_CD = 'A3'`, 200 alanlı DTO) kodunuza sızarsa domain'iniz **bozulur (corrupt)**.

### Çözüm (DDD — Eric Evans)
> İki model arasına, dış modeli kendi ubiquitous language'ınıza çeviren bir **çeviri katmanı** koyun.

```csharp
// Domain'in istediği arayüz (kendi dilimizde)
public interface ICustomerCreditProvider { Task<CreditLimit> GetLimitAsync(CustomerId id); }

// ACL: Legacy SOAP modelini domain modeline çevirir
internal sealed class LegacyErpCreditAdapter(ErpSoapClient erp) : ICustomerCreditProvider
{
    public async Task<CreditLimit> GetLimitAsync(CustomerId id)
    {
        var resp = await erp.GETCUSTCRDAsync(new GETCUSTCRDRequest { CUST_NO = id.Value.ToString("N")[..10] });
        return resp.STAT_CD switch
        {
            "A1" or "A3" => new CreditLimit(decimal.Parse(resp.CRD_LMT, CultureInfo.InvariantCulture), Currency.TRY),
            "B9"         => CreditLimit.Blocked,
            _            => CreditLimit.None
        };
    }
}
```

Facade + Adapter + Translator birleşimidir. Hexagonal mimarideki **driven adapter**'ın özel bir halidir. Strangler Fig geçişlerinde de kritik rol oynar.

---

## 20.4 Diğer İlgili Desenler (Kısa)

| Desen | Problem | Çözüm |
|-------|---------|-------|
| **Shared Database** (anti-pattern) | Hızlı başlangıç | Kısa vadede kolay, uzun vadede coupling. Sadece geçiş döneminde. |
| **Change Data Capture (CDC)** | Legacy DB değişikliklerini olaya çevirmek | Debezium, SQL Server CDC → Kafka |
| **Materialized View** | Pahalı sorgular | Önceden hesaplanmış görünüm, olaylarla güncellenir |
| **Sidecar** | Her serviste tekrar eden altyapı kodu (mTLS, retry, telemetri) | Yanında çalışan yardımcı process (Dapr, Envoy/Istio) |
| **Service Mesh** | Servisler arası ağ yönetimi | Istio, Linkerd: mTLS, trafik yönetimi, gözlemlenebilirlik |
| **Competing Consumers** | Tek tüketici yetişemiyor | Aynı kuyruğu birden fazla instance dinler |
| **Claim Check** | Büyük mesaj broker'ı tıkıyor | Payload'u blob'a koy, mesajda referans gönder |
| **Correlation ID** | Dağıtık akışı izleyememek | Her isteğe/mesaja taşınan ID; OpenTelemetry `traceparent` |
| **Health Check API** | Orkestratör servisin durumunu bilmiyor | `/health/live`, `/health/ready` (`AddHealthChecks()`) |
