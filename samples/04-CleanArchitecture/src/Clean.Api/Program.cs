// [Api Katmanı – Composition Root / Frameworks & Drivers]
// Tek görevi: katmanları birbirine bağlamak (DI) ve HTTP'yi use case'lere (ISender) yönlendirmek.
// İş kuralı yok, EF Core kullanımı yok. Infrastructure referansı SADECE AddInfrastructure() için var.
using Clean.Api.Endpoints;
using Clean.Api.Infrastructure;
using Clean.Application;
using Clean.Infrastructure;
using Clean.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler();

await app.Services.InitialiseDatabaseAsync();

app.MapGet("/", () => Results.Redirect("/products"));
app.MapOrderEndpoints();
app.MapProductEndpoints();

app.Run();

// WebApplicationFactory<Program> için (integration testler).
public partial class Program;
