using ModularMonolith.Modules.Catalog;
using ModularMonolith.Modules.Ordering;
using ModularMonolith.Modules.Shipping;

// [Bootstrapper] Tek process / tek deploy. Modülleri yükler; başka HİÇBİR iş mantığı içermez.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services
    .AddCatalogModule()
    .AddOrderingModule()
    .AddShippingModule();

var app = builder.Build();

app.UseExceptionHandler();
app.MapCatalogEndpoints();
app.MapOrderingEndpoints();
app.MapShippingEndpoints();

app.Run();
