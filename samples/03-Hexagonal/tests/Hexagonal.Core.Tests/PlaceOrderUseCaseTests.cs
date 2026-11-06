using Hexagonal.Core.Domain;
using Hexagonal.Core.Exceptions;
using Hexagonal.Core.Ports.In;
using Hexagonal.Core.Ports.Out;
using Hexagonal.Core.Tests.TestAdapters;
using Hexagonal.Core.UseCases;

namespace Hexagonal.Core.Tests;

// Bu test sınıfı üçüncü bir DRIVING adapter gibi davranır: Core'u IPlaceOrderUseCase port'u üzerinden sürer.
// Driven tarafta gerçek adapter'lar yerine in-memory test adapter'ları takılı.
public class PlaceOrderUseCaseTests
{
    private readonly InMemoryOrderRepository _orders = new();
    private readonly RecordingNotifier _notifier = new();

    private IPlaceOrderUseCase CreateUseCase(IPaymentGateway payments) =>
        new PlaceOrderService(new InMemoryProductCatalog(), payments, _orders, _notifier);

    private static PlaceOrderCommand KlavyeX2() =>
        new(Guid.NewGuid(), [new PlaceOrderItem(InMemoryProductCatalog.Klavye.Id, 2)]);

    [Fact]
    public async Task Places_order_charges_payment_and_notifies()
    {
        var payments = new AlwaysApprovePaymentGateway();

        var id = await CreateUseCase(payments).ExecuteAsync(KlavyeX2());

        var order = _orders.Orders[id];
        Assert.Equal(OrderStatus.Placed, order.Status);
        Assert.Equal(1500m, order.Total);
        Assert.Equal("test-tx-1", order.PaymentTransactionId);
        Assert.Equal([1500m], payments.ChargedAmounts);
        Assert.Single(_notifier.Notifications, n => n.OrderId == id);
    }

    [Fact]
    public async Task Declined_payment_throws_and_nothing_is_saved_or_notified()
    {
        var useCase = CreateUseCase(new AlwaysDeclinePaymentGateway());

        await Assert.ThrowsAsync<PaymentDeclinedException>(() => useCase.ExecuteAsync(KlavyeX2()));

        Assert.Empty(_orders.Orders);
        Assert.Empty(_notifier.Notifications);
    }

    [Fact]
    public async Task Unknown_product_throws_domain_exception_before_charging()
    {
        var payments = new AlwaysApprovePaymentGateway();
        var command = new PlaceOrderCommand(Guid.NewGuid(), [new PlaceOrderItem(Guid.NewGuid(), 1)]);

        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase(payments).ExecuteAsync(command));

        Assert.Empty(payments.ChargedAmounts);
    }

    [Fact]
    public async Task Empty_order_is_rejected_by_domain()
    {
        var command = new PlaceOrderCommand(Guid.NewGuid(), []);
        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase(new AlwaysApprovePaymentGateway()).ExecuteAsync(command));
    }
}
