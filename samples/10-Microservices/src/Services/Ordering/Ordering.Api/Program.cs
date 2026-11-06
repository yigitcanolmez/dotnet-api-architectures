// [Ordering Servisi — Composition Root] Port 5101, DB: ordering.db
// Sorumluluk: siparişi almak ve sipariş SAGA'sını orkestre etmek.
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Ordering.Api.Consumers;
using Ordering.Api.Endpoints;
using Ordering.Api.Infrastructure;
using Ordering.Api.Saga;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; });

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks(); // MassTransit bus sağlık kontrolünü buraya otomatik ekler.
builder.Services.AddDbContext<OrderingDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Ordering")));

builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();

    // Transactional Outbox (+ consumer tarafında Inbox). SQLite için MassTransit'in yerleşik lock provider'ı var.
    x.AddEntityFrameworkOutbox<OrderingDbContext>(o =>
    {
        o.UseSqlite();
        o.UseBusOutbox();                          // HTTP handler'daki Publish'leri outbox'a yönlendir
        o.QueryDelay = TimeSpan.FromSeconds(1);    // Teslimat servisi outbox'ı ne sıklıkla tarasın
        o.DuplicateDetectionWindow = TimeSpan.FromMinutes(30); // Inbox: bu süre içinde aynı MessageId tekrar işlenmez
    });

    // Her receive endpoint'e: retry + EF outbox/inbox. Sıra önemli: retry dışta, outbox içte.
    x.AddConfigureEndpointsCallback((context, _, cfg) =>
    {
        cfg.UseMessageRetry(r => r.Intervals(100, 500, 1000, 2000, 5000));
        cfg.UseEntityFrameworkOutbox<OrderingDbContext>(context);
    });

    x.AddSagaStateMachine<OrderStateMachine, OrderState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.ExistingDbContext<OrderingDbContext>();
            r.UseSqlite();
        });

    x.AddConsumer<OrderConfirmedConsumer>();
    x.AddConsumer<OrderRejectedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbit = builder.Configuration.GetSection("RabbitMq");
        cfg.Host(rabbit["Host"] ?? "localhost", rabbit["VirtualHost"] ?? "/", h =>
        {
            h.Username(rabbit["Username"] ?? "guest");
            h.Password(rabbit["Password"] ?? "guest");
        });
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

await DatabaseInitializer.InitializeAsync(app.Services);

app.UseExceptionHandler();
app.MapHealthChecks("/health");
app.MapOrderEndpoints();

app.Run();
