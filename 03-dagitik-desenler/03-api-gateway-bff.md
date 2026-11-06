# 17. API Gateway & Backend for Frontend (BFF)

> 💻 **Çalışan örnek proje:** [`samples/10-Microservices`](../samples/10-Microservices/)

## API Gateway

### Neden Ortaya Çıktı?

Mikroservislere geçince istemci (web, mobil):
- 15 farklı servisin adresini bilmek zorunda kaldı.
- Bir ekran için 6 ayrı HTTP çağrısı yaptı (mobilde yavaş, pil tüketir).
- Her servis ayrı ayrı auth, rate limiting, CORS, SSL, logging uyguladı (tekrar).
- İç servis yapısı değişince istemciler kırıldı.
- İç servisler internete açık kaldı (güvenlik riski).

### Çözdüğü Problem

> "İstemciler için **tek giriş noktası** sun; yönlendirme ve ortak kesişen kaygıları (cross-cutting concerns) merkezde çöz."

```
 Web ─┐                ┌──────────────────────────┐      ┌─► Catalog
 iOS ─┼──► HTTPS ────► │       API Gateway        │──────┼─► Ordering
 3rd ─┘                │ Routing · Auth (JWT)     │      ├─► Payment
                       │ Rate limit · Caching     │      └─► Identity
                       │ SSL termination · CORS   │
                       │ Request aggregation      │
                       │ Logging · Tracing        │
                       └──────────────────────────┘
```

### Sorumluluklar
- **Routing / Reverse proxy**: `/api/orders/*` → ordering-svc
- **Authentication**: Token doğrulama (yetkilendirme detayı serviste kalır)
- **Rate limiting / throttling**
- **Request aggregation**: Birden fazla servisi çağırıp tek yanıt
- **Protocol translation**: REST ↔ gRPC
- **Caching, compression, SSL termination**
- **Load balancing, health checks**

### .NET — YARP (Yet Another Reverse Proxy)

```csharp
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddRateLimiter(o => o.AddFixedWindowLimiter("fixed", x => { x.PermitLimit = 100; x.Window = TimeSpan.FromMinutes(1); }));

var app = builder.Build();
app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
app.MapReverseProxy();
```

```json
"ReverseProxy": {
  "Routes": {
    "orders": { "ClusterId": "ordering", "Match": { "Path": "/api/orders/{**rest}" },
                "AuthorizationPolicy": "default", "RateLimiterPolicy": "fixed",
                "Transforms": [ { "PathPattern": "/orders/{**rest}" } ] }
  },
  "Clusters": {
    "ordering": { "Destinations": { "d1": { "Address": "http://ordering-svc" } },
                  "LoadBalancingPolicy": "RoundRobin",
                  "HealthCheck": { "Active": { "Enabled": true, "Path": "/health" } } }
  }
}
```

Alternatifler: **Ocelot**, **Azure API Management**, **Kong**, **Envoy**, **NGINX**, **AWS API Gateway**.

### Riskler
- ❌ Tek hata noktası → yüksek erişilebilirlik ile çalıştırılmalı.
- ❌ İş mantığı gateway'e sızarsa → "ESB'nin yeniden doğuşu". Gateway **aptal** kalmalı.
- ❌ Tek gateway, tüm istemci türlerine hizmet ederken şişer → **BFF**.

---

## Backend for Frontend (BFF)

> Sam Newman / SoundCloud, 2015

### Neden Ortaya Çıktı?

Tek bir genel amaçlı gateway/API:
- Mobil az veri ister, web çok veri ister → herkes için ortalama, kimse için ideal değil.
- Mobil ekip bir alan eklemek için web ekibini/gateway ekibini bekler.
- SPA'larda token'ları tarayıcıda saklamak güvenlik riski (XSS).

### Çözdüğü Problem

> "Her istemci türü için **ayrı, o istemcinin ekibine ait** bir backend katmanı oluştur."

```
 Web SPA   ──► Web BFF    ─┐
 Mobile    ──► Mobile BFF ─┼──► Mikroservisler
 Partner   ──► Public API ─┘
```

### Güvenlik BFF'i (SPA için)
Modern öneri (OAuth 2.0 for Browser-Based Apps): SPA token tutmaz; BFF OIDC akışını yürütür, tarayıcıya **HttpOnly, Secure, SameSite cookie** verir, servislere token'ı BFF ekler. .NET'te: **Duende BFF**, YARP + `Microsoft.Identity.Web`.

### Artıları
- ✅ İstemciye özel optimize yanıtlar.
- ✅ Ekip bağımsızlığı.
- ✅ Daha güvenli SPA mimarisi.

### Eksileri
- ❌ Kod tekrarı (BFF'ler arası).
- ❌ Daha fazla deploy birimi.

## Ne Zaman Kullanılır?
- **Gateway**: Birden fazla servis dışarı açılıyorsa neredeyse her zaman.
- **BFF**: Belirgin şekilde farklı ihtiyaçları olan birden fazla istemci türü varsa, veya SPA güvenliği için.
