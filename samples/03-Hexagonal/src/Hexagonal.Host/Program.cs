using Hexagonal.Adapters.Messaging;
using Hexagonal.Adapters.Notifications;
using Hexagonal.Adapters.Payments;
using Hexagonal.Adapters.Persistence;
using Hexagonal.Adapters.Web;
using Hexagonal.Core.Ports.In;
using Hexagonal.Core.Ports.Out;
using Hexagonal.Core.UseCases;

// [Host / Wiring] Port ↔ Adapter eşlemelerinin TAMAMI bu dosyada görülebilir.
var builder = WebApplication.CreateBuilder(args);

// ── Inbound (driving) port'lar → Core use case implementasyonları
builder.Services.AddScoped<IPlaceOrderUseCase, PlaceOrderService>();
builder.Services.AddScoped<ICancelOrderUseCase, CancelOrderService>();
builder.Services.AddScoped<IShipOrderUseCase, ShipOrderService>();
builder.Services.AddScoped<IGetOrderQuery, GetOrderQueryService>();
builder.Services.AddScoped<IListProductsQuery, ListProductsQueryService>();

// ── Outbound (driven) port'lar → driven adapter'lar
builder.Services.AddPersistenceAdapter(builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default eksik."));
builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
builder.Services.AddScoped<IProductCatalog, EfProductCatalog>();
builder.Services.AddSingleton<IPaymentGateway, FakePaymentGateway>();
builder.Services.AddSingleton<INotifier, LoggerNotifier>();

// ── Driving adapter'lar
builder.Services.AddWebAdapter();        // HTTP
builder.Services.AddMessagingAdapter();  // in-memory kuyruk + BackgroundService consumer

var app = builder.Build();

app.Services.EnsurePersistenceCreated();
app.UseWebAdapter();

// Dış sistem simülasyonu: "başka bir uygulama kuyruğa sipariş mesajı bırakıyor".
// Bu endpoint use case'i ÇAĞIRMAZ; sadece mesajı kuyruğa koyar. Use case'i Messaging adapter'ı tetikler.
app.MapPost("/queue/orders", async (PlaceOrderMessageRequest request, InMemoryOrderQueue queue, CancellationToken ct) =>
{
    var message = new PlaceOrderMessage(Guid.NewGuid(), request.CustomerId,
        (request.Items ?? []).Select(i => new PlaceOrderMessageItem(i.ProductId, i.Quantity)).ToList());
    await queue.PublishAsync(message, ct);
    return Results.Accepted(value: new { messageId = message.MessageId, info = "Mesaj kuyruğa bırakıldı; sonucu loglarda ve GET /orders'ta görün." });
}).WithTags("Queue (simülasyon)");

app.Run();

record PlaceOrderMessageRequest(Guid CustomerId, List<PlaceOrderMessageItem>? Items);
