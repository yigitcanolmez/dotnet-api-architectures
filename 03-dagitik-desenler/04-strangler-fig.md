# 18. Strangler Fig Deseni

> 💻 **Çalışan örnek proje:** [`samples/11-StranglerFig`](../samples/11-StranglerFig/)

> Martin Fowler, 2004 — adını, ev sahibi ağacı yavaşça saran ve sonunda yerini alan boğucu incir ağacından alır.

## Neden Ortaya Çıktı?

Legacy sistemi (ör. .NET Framework 4.x WebForms/WCF monoliti) modernize etmek gerekiyor. **"Big Bang Rewrite"** (sıfırdan yeniden yazıp bir günde geçiş):
- Yıllar sürer; bu sürede iş durmaz, legacy'ye yeni özellik eklenmeye devam eder → hedef sürekli kayar.
- Legacy'deki **belgelenmemiş iş kuralları** kaybolur.
- Geçiş günü devasa risk; geri dönüş zor.
- Değer, ancak proje bittiğinde (eğer biterse) üretilir.

## Çözdüğü Problem

> "Legacy sistemi **parça parça** yeni sistemle değiştir. Önüne bir yönlendirme katmanı (facade/proxy) koy; taşınan özelliklerin trafiğini yeni sisteme, kalanları eskiye yönlendir. Eski sistem tamamen 'boğulunca' kapat."

```
Faz 1                    Faz 2                       Faz 3
Client                   Client                      Client
  │                        │                           │
[Proxy]                  [Proxy]                     [Proxy]
  │                       │    │                         │
Legacy (100%)          Legacy  New (/orders)           New (100%)
                       (kalan)                         Legacy ⚰️
```

## .NET'te Uygulama

### 1) YARP ile yönlendirme
```json
"Routes": {
  "orders-new": { "ClusterId": "modern", "Order": 1, "Match": { "Path": "/api/orders/{**r}" } },
  "fallback":   { "ClusterId": "legacy", "Order": 100, "Match": { "Path": "{**catch-all}" } }
}
```

### 2) Microsoft'un "Incremental ASP.NET Migration" (System.Web Adapters)
ASP.NET Core uygulaması öne konur; bulamadığı route'ları YARP ile eski ASP.NET Framework uygulamasına proxy'ler. Session ve kimlik doğrulama iki uygulama arasında paylaşılabilir.

```csharp
builder.Services.AddSystemWebAdapters()
    .AddRemoteAppClient(o => { o.RemoteAppUrl = new(builder.Configuration["LegacyUrl"]!); o.ApiKey = "..."; })
    .AddSessionClient();
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
// ...
app.MapDefaultControllerRoute();
app.MapForwarder("/{**catch-all}", app.Configuration["LegacyUrl"]!).Add(b => ((RouteEndpointBuilder)b).Order = int.MaxValue);
```

### 3) Veri senkronizasyonu
Geçiş döneminde iki sistem aynı veriye ihtiyaç duyabilir: CDC, olay yayını, veya yeni sistemin legacy DB'yi okuması (geçici). Yeni modeli legacy modelden korumak için **Anti-Corruption Layer** kullanılır.

## Adımlar
1. Proxy/facade'ı koy (henüz her şey legacy'ye gider).
2. Taşınacak ilk dilimi seç (düşük risk, yüksek değer, net sınır).
3. Yeni sistemde yaz, test et (gerekirse shadow traffic / feature flag).
4. Trafiği yönlendir, izle.
5. Legacy'deki ilgili kodu sil.
6. Tekrarla.

## Artıları / Eksileri
- ✅ Düşük risk, sürekli değer üretimi, geri alınabilir.
- ❌ Geçiş süresince iki sistemi birlikte işletme maliyeti; veri senkronizasyonu karmaşık; "hiç bitmeyen geçiş" riski.
