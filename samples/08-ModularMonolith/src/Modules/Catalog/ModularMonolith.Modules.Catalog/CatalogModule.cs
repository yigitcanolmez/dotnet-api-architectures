using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Catalog.Api;
using ModularMonolith.Modules.Catalog.Contracts;
using ModularMonolith.Modules.Catalog.Features.ListProducts;
using ModularMonolith.Modules.Catalog.Features.ReleaseStock;
using ModularMonolith.Modules.Catalog.Features.ReserveStock;
using ModularMonolith.Modules.Catalog.Infrastructure;
using ModularMonolith.Modules.Ordering.Contracts;
using ModularMonolith.Shared.Infrastructure.Modules;

namespace ModularMonolith.Modules.Catalog;

// [Catalog — Modül giriş noktası] Bu assembly'deki TEK public tip. Bootstrapper modülü sadece bununla tanır.
public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<CatalogDbContext>("Catalog", CatalogSeed.SeedAsync);

        // Senkron public API
        services.AddScoped<ICatalogModuleApi, CatalogModuleApi>();

        // Asenkron: dinlenen integration event'ler
        services.AddIntegrationEventHandler<OrderPlacedIntegrationEvent, ReserveStockOnOrderPlacedHandler>();
        services.AddIntegrationEventHandler<OrderCancelledIntegrationEvent, ReleaseStockOnOrderCancelledHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        ListProducts.Map(app);
        return app;
    }
}
