using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModularMonolith.Modules.Ordering.Contracts;
using ModularMonolith.Modules.Shipping.Features.CancelShipment;
using ModularMonolith.Modules.Shipping.Features.CreateShipment;
using ModularMonolith.Modules.Shipping.Features.GetShipmentByOrder;
using ModularMonolith.Modules.Shipping.Features.ListShipments;
using ModularMonolith.Modules.Shipping.Features.MarkShipmentShipped;
using ModularMonolith.Modules.Shipping.Infrastructure;
using ModularMonolith.Shared.Infrastructure.Modules;

namespace ModularMonolith.Modules.Shipping;

// [Shipping — Modül giriş noktası] Bu assembly'deki TEK public tip.
public static class ShippingModule
{
    public static IServiceCollection AddShippingModule(this IServiceCollection services)
    {
        services.AddModuleDbContext<ShippingDbContext>("Shipping");

        services.AddIntegrationEventHandler<OrderPlacedIntegrationEvent, CreateShipmentOnOrderPlacedHandler>();
        services.AddIntegrationEventHandler<OrderShippedIntegrationEvent, MarkShipmentShippedOnOrderShippedHandler>();
        services.AddIntegrationEventHandler<OrderCancelledIntegrationEvent, CancelShipmentOnOrderCancelledHandler>();
        return services;
    }

    public static IEndpointRouteBuilder MapShippingEndpoints(this IEndpointRouteBuilder app)
    {
        ListShipments.Map(app);
        GetShipmentByOrder.Map(app);
        return app;
    }
}
