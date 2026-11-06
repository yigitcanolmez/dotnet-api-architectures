using MassTransit;
using SagaChoreography.Services.Inventory;
using SagaChoreography.Services.Ordering;
using SagaChoreography.Services.Payment;

// [Host] ÖĞRETİM AMAÇLI: Üç "servis" (Ordering, Inventory, Payment) TEK process'te, ama her biri kendi klasöründe,
// kendi DbContext'i/SQLite dosyası ile ve birbirleriyle SADECE mesajlarla konuşacak şekilde simüle ediliyor.
// Broker olarak MassTransit in-memory transport kullanılıyor → RabbitMQ kurmadan çalışır.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services
    .AddOrderingService()
    .AddInventoryService()
    .AddPaymentService();

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    // Her servis kendi consumer'larını kaydeder; aralarında merkezi bir koordinatör (orchestrator) YOK.
    x.AddOrderingConsumers();
    x.AddInventoryConsumers();
    x.AddPaymentConsumers();

    // Transactional Outbox (sadece Ordering HTTP endpoint'leri için): API'den Publish edilen mesajlar
    // ordering.db'deki OutboxMessage tablosuna, sipariş ile aynı transaction'da yazılır.
    x.AddEntityFrameworkOutbox<OrderingDbContext>(o =>
    {
        o.UseSqlite();
        o.UseBusOutbox();
        o.QueryDelay = TimeSpan.FromMilliseconds(200);
    });

    // Tüm consumer endpoint'leri için: retry (geçici hatalar, concurrency çakışmaları) +
    // in-memory outbox (consumer başarıyla bitmeden Publish edilen mesajlar gönderilmez).
    x.AddConfigureEndpointsCallback((context, _, cfg) =>
    {
        cfg.UseMessageRetry(r => r.Intervals(100, 250, 500, 1000, 2000));
        cfg.UseInMemoryOutbox(context);
    });

    x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
});

var app = builder.Build();

app.UseExceptionHandler();

// Veritabanları: EnsureCreated + seed (migration yok — öğretim amaçlı sadelik).
await using (var scope = app.Services.CreateAsyncScope())
{
    var ordering = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
    await ordering.Database.EnsureCreatedAsync();
    await ordering.SeedAsync();

    var inventory = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    await inventory.Database.EnsureCreatedAsync();
    await inventory.SeedAsync();

    await scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.EnsureCreatedAsync();
}

app.MapOrderingEndpoints();
app.MapInventoryEndpoints();
app.MapPaymentEndpoints();

app.Run();
