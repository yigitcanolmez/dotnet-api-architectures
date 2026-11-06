using Cqrs.Api.Common;
using Cqrs.Api.Endpoints;
using Cqrs.Api.Projections;
using Cqrs.Api.Read;
using Cqrs.Api.Write;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Default")!;

// ── Yazma tarafı: EF Core + zengin domain (normalize tablolar)
builder.Services.AddDbContext<WriteDbContext>(o => o.UseSqlite(connectionString));

// ── Okuma tarafı: Dapper + ham SQL (denormalize tablolar, aynı DB dosyası)
builder.Services.AddSingleton(new ReadDb(connectionString));

// ── Senkronizasyon: Write → Channel → ProjectionWorker → Projektörler → Read
builder.Services.AddSingleton<ProjectionChannel>();
builder.Services.AddSingleton<IEventPublisher, ChannelEventPublisher>();
builder.Services.AddScoped<IProjector, OrderSummaryProjector>();
builder.Services.AddScoped<IProjector, CustomerStatsProjector>();
builder.Services.Configure<ProjectionOptions>(builder.Configuration.GetSection("Projection"));
builder.Services.AddHostedService<ProjectionWorker>();

// ── Komut/sorgu handler'ları (MediatR yok; reflection ile kayıt)
builder.Services.AddCqrsHandlers(typeof(Program).Assembly);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

using (var scope = app.Services.CreateScope())
{
    await Seed.InitializeAsync(scope.ServiceProvider.GetRequiredService<WriteDbContext>());
    scope.ServiceProvider.GetRequiredService<ReadDb>().EnsureSchema();
}

app.MapCommandEndpoints();
app.MapQueryEndpoints();

app.Run();

public partial class Program;
