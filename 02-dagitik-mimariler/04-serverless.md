# 14. Serverless Mimari

> AWS Lambda (2014), Azure Functions (2016)

## Neden Ortaya Çıktı?

- Sunucu/VM/container yönetimi (patch, ölçekleme, kapasite planlama) işe değer katmayan yüktür.
- Pek çok iş yükü **düzensizdir**: gece hiç trafik yok, kampanyada 1000x artış. 7/24 açık sunucu için para ödemek israftır.
- Olay tetiklemeli küçük işler (dosya yüklendi → thumbnail üret) için tam bir API sunucusu ayağa kaldırmak gereksiz.

## Çözdüğü Problem

> "Altyapıyı düşünme; kodu yükle, olay geldiğinde çalışsın, sadece kullandığın kadar öde, ölçekleme otomatik (sıfıra kadar)."

## Bileşenler

- **FaaS (Function as a Service)**: Azure Functions, AWS Lambda
- **BaaS (Backend as a Service)**: Cosmos DB serverless, DynamoDB, Blob Storage, Auth (Entra ID B2C/Cognito)
- **Serverless container**: Azure Container Apps (scale-to-zero), AWS Fargate, Cloud Run
- **Orkestrasyon**: Durable Functions, AWS Step Functions

## .NET Örneği — Azure Functions (Isolated Worker)

```csharp
public class OrderFunctions(OrderingDbContext db, ILogger<OrderFunctions> log)
{
    [Function("CreateOrder")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "orders")] HttpRequest req)
    {
        var cmd = await req.ReadFromJsonAsync<CreateOrder>();
        // ...
        return new CreatedResult($"/orders/{id}", id);
    }

    [Function("OnOrderPlaced")]
    public async Task OnPlaced([ServiceBusTrigger("order-placed", Connection = "Sb")] OrderPlaced evt)
        => log.LogInformation("Order {Id} placed", evt.OrderId);

    [Function("NightlyReport")]
    public Task Nightly([TimerTrigger("0 0 2 * * *")] TimerInfo t) => Task.CompletedTask;
}
```

### Durable Functions — Serverless Saga/Orkestrasyon

```csharp
[Function(nameof(OrderSaga))]
public static async Task OrderSaga([OrchestrationTrigger] TaskOrchestrationContext ctx)
{
    var order = ctx.GetInput<OrderDto>()!;
    await ctx.CallActivityAsync("ReserveStock", order);
    try
    {
        await ctx.CallActivityAsync("ChargePayment", order);
    }
    catch (TaskFailedException)
    {
        await ctx.CallActivityAsync("ReleaseStock", order);   // compensation
        throw;
    }
    await ctx.CallActivityAsync("ShipOrder", order);
}
```

## Artıları

- ✅ Sıfır sunucu yönetimi.
- ✅ Otomatik ölçekleme (sıfırdan binlere).
- ✅ Kullandığın kadar öde — düzensiz yüklerde çok ucuz.
- ✅ Olay tetiklemeli işler için doğal uyum.

## Eksileri

- ❌ **Cold start** (.NET'te Native AOT / Premium plan / pre-warmed ile azaltılır).
- ❌ Çalışma süresi limitleri.
- ❌ Vendor lock-in (trigger/binding modeli platforma özel).
- ❌ Lokal geliştirme, debug ve test zorluğu.
- ❌ Sürekli yüksek yükte container/VM'den **pahalı** olabilir.
- ❌ Durum yönetimi (stateless zorunlu; state dışarıda).

## Ne Zaman Kullanılır?

- Olay tetiklemeli arka plan işleri (dosya işleme, webhook, kuyruk tüketici)
- Zamanlanmış görevler
- Düzensiz/düşük trafikli API'ler
- Mikroservis sistemlerinde yardımcı işler

> Serverless bir **dağıtım modeli**dir; içindeki kod yine VSA / Clean gibi organize edilebilir.
