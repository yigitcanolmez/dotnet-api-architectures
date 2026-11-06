using MassTransit;
using Microsoft.EntityFrameworkCore;
using SagaChoreography.BuildingBlocks;
using SagaChoreography.Services.Inventory.Consumers;

namespace SagaChoreography.Services.Inventory;

public static class InventoryService
{
    public static IServiceCollection AddInventoryService(this IServiceCollection services) =>
        services.AddServiceDbContext<InventoryDbContext>("Inventory");

    public static void AddInventoryConsumers(this IBusRegistrationConfigurator bus)
    {
        bus.AddConsumer<ReserveStockOnOrderCreatedConsumer>();
        bus.AddConsumer<ReleaseStockOnPaymentFailedConsumer>();
        bus.AddConsumer<ReleaseStockOnOrderCancelledConsumer>();
    }

    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/inventory", async (InventoryDbContext db) => Results.Ok(new
        {
            stocks = await db.Stocks.AsNoTracking().OrderBy(s => s.ProductName)
                .Select(s => new { s.ProductId, s.ProductName, s.Available }).ToListAsync(),
            reservations = (await db.Reservations.AsNoTracking().ToListAsync())
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new { r.OrderId, Status = r.Status.ToString(), r.Lines })
        }));
        return app;
    }
}
