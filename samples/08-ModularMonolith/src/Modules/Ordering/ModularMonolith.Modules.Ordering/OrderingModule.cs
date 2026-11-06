using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Catalog.Contracts;
using ModularMonolith.Modules.Ordering.Features.CancelOrder;
using ModularMonolith.Modules.Ordering.Features.CancelOrderOnStockReservationFailed;
using ModularMonolith.Modules.Ordering.Features.GetOrder;
using ModularMonolith.Modules.Ordering.Features.ListOrders;
using ModularMonolith.Modules.Ordering.Features.PlaceOrder;
using ModularMonolith.Modules.Ordering.Features.ShipOrder;
using ModularMonolith.Modules.Ordering.Infrastructure;
using ModularMonolith.Shared.Infrastructure.Modules;

namespace ModularMonolith.Modules.Ordering;

// [Ordering — Modül giriş noktası] Bu assembly'deki TEK public tip.
public static class OrderingModule
{
    public static IServiceCollection AddOrderingModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<OrderingDbContext>("Ordering");

        services.AddScoped<PlaceOrder.Handler>();
        services.AddScoped<ShipOrder.Handler>();
        services.AddScoped<CancelOrder.Handler>();

        services.AddIntegrationEventHandler<StockReservationFailedIntegrationEvent, CancelOrderOnStockReservationFailedHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapOrderingEndpoints(this IEndpointRouteBuilder app)
    {
        PlaceOrder.Map(app);
        ListOrders.Map(app);
        GetOrder.Map(app);
        ShipOrder.Map(app);
        CancelOrder.Map(app);
        return app;
    }
}
