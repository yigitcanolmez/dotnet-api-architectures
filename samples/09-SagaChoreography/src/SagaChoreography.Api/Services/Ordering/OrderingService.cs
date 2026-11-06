using MassTransit;
using SagaChoreography.BuildingBlocks;
using SagaChoreography.Services.Ordering.Consumers;

namespace SagaChoreography.Services.Ordering;

// Ordering "servisinin" kaydı. Gerçek dağıtık sistemde bu, ayrı bir host'un Program.cs'i olurdu.
public static class OrderingService
{
    public static IServiceCollection AddOrderingService(this IServiceCollection services) =>
        services.AddServiceDbContext<OrderingDbContext>("Ordering");

    public static void AddOrderingConsumers(this IBusRegistrationConfigurator bus)
    {
        bus.AddConsumer<ConfirmOrderOnPaymentCompletedConsumer>();
        bus.AddConsumer<RejectOrderOnStockReservationFailedConsumer>();
        bus.AddConsumer<RejectOrderOnPaymentFailedConsumer>();
    }
}
