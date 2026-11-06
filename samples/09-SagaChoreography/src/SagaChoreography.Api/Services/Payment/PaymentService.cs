using MassTransit;
using Microsoft.EntityFrameworkCore;
using SagaChoreography.BuildingBlocks;
using SagaChoreography.Services.Payment.Consumers;

namespace SagaChoreography.Services.Payment;

public static class PaymentService
{
    public static IServiceCollection AddPaymentService(this IServiceCollection services) =>
        services.AddServiceDbContext<PaymentDbContext>("Payment");

    public static void AddPaymentConsumers(this IBusRegistrationConfigurator bus)
    {
        bus.AddConsumer<ChargePaymentOnStockReservedConsumer>();
        bus.AddConsumer<RefundPaymentOnOrderCancelledConsumer>();
    }

    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/payments", async (PaymentDbContext db) =>
            Results.Ok((await db.Payments.AsNoTracking().ToListAsync())
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new { p.Id, p.OrderId, p.Amount, Status = p.Status.ToString(), p.FailureReason, p.CreatedAt })));
        return app;
    }
}
