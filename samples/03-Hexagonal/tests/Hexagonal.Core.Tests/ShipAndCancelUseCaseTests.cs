using Hexagonal.Core.Domain;
using Hexagonal.Core.Exceptions;
using Hexagonal.Core.Ports.In;
using Hexagonal.Core.Tests.TestAdapters;
using Hexagonal.Core.UseCases;

namespace Hexagonal.Core.Tests;

public class ShipAndCancelUseCaseTests
{
    private readonly InMemoryOrderRepository _orders = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly IShipOrderUseCase _ship;
    private readonly ICancelOrderUseCase _cancel;
    private readonly IGetOrderQuery _query;
    private readonly IPlaceOrderUseCase _place;

    public ShipAndCancelUseCaseTests()
    {
        _place = new PlaceOrderService(new InMemoryProductCatalog(), new AlwaysApprovePaymentGateway(), _orders, _notifier);
        _ship = new ShipOrderService(_orders, _notifier);
        _cancel = new CancelOrderService(_orders, _notifier);
        _query = new GetOrderQueryService(_orders);
    }

    private Task<Guid> PlaceAsync() =>
        _place.ExecuteAsync(new PlaceOrderCommand(Guid.NewGuid(), [new PlaceOrderItem(InMemoryProductCatalog.Klavye.Id, 1)]));

    [Fact]
    public async Task Ship_then_cancel_is_rejected()
    {
        var id = await PlaceAsync();
        await _ship.ExecuteAsync(id);

        var ex = await Assert.ThrowsAsync<DomainException>(() => _cancel.ExecuteAsync(id));
        Assert.Contains("iptal edilemez", ex.Message);
        Assert.Equal("Shipped", (await _query.GetAsync(id))!.Status);
    }

    [Fact]
    public async Task Cancel_placed_order_succeeds()
    {
        var id = await PlaceAsync();

        await _cancel.ExecuteAsync(id);

        Assert.Equal(OrderStatus.Cancelled, _orders.Orders[id].Status);
        Assert.Contains(_notifier.Notifications, n => n.OrderId == id && n.Message.Contains("iptal"));
    }

    [Fact]
    public async Task Cancelled_order_cannot_be_shipped_or_cancelled_again()
    {
        var id = await PlaceAsync();
        await _cancel.ExecuteAsync(id);

        await Assert.ThrowsAsync<DomainException>(() => _ship.ExecuteAsync(id));
        await Assert.ThrowsAsync<DomainException>(() => _cancel.ExecuteAsync(id));
    }

    [Fact]
    public async Task Missing_order_throws_not_found_and_query_returns_null()
    {
        await Assert.ThrowsAsync<OrderNotFoundException>(() => _cancel.ExecuteAsync(Guid.NewGuid()));
        Assert.Null(await _query.GetAsync(Guid.NewGuid()));
    }
}
