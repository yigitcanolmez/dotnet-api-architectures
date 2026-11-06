using Onion.Application.Dtos;
using Onion.Application.Exceptions;
using Onion.Application.Orders;
using Onion.Application.Tests.Fakes;
using Onion.Domain.Entities;
using Onion.Domain.Exceptions;
using Onion.Domain.Services;

namespace Onion.Application.Tests;

// Application servisini Infrastructure OLMADAN test ediyoruz: sadece el yazımı fake'ler.
public class OrderAppServiceTests
{
    private static readonly Product Klavye = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Klavye", 750m, 10);

    private readonly FakeOrderRepository _orders = new();
    private readonly FakeNotificationService _notifications = new();
    private readonly OrderAppService _sut;

    public OrderAppServiceTests()
    {
        var placement = new OrderPlacementService(new FakeProductRepository(Klavye));
        _sut = new OrderAppService(_orders, placement, _notifications);
    }

    private Task<Guid> PlaceKlavyeAsync() =>
        _sut.PlaceAsync(new PlaceOrderRequest(Guid.NewGuid(), [new(Klavye.Id, 2)]));

    [Fact]
    public async Task PlaceAsync_saves_order_and_sends_notification()
    {
        var id = await PlaceKlavyeAsync();

        Assert.True(_orders.Store.ContainsKey(id));
        Assert.Equal(1500m, _orders.Store[id].Total);
        Assert.Equal(1, _orders.SaveChangesCalls);
        Assert.Contains($"placed:{id}", _notifications.Sent);
    }

    [Fact]
    public async Task PlaceAsync_with_unknown_product_throws_and_saves_nothing()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.PlaceAsync(new PlaceOrderRequest(Guid.NewGuid(), [new(Guid.NewGuid(), 1)])));

        Assert.Empty(_orders.Store);
        Assert.Empty(_notifications.Sent);
    }

    [Fact]
    public async Task CancelAsync_cancels_and_notifies()
    {
        var id = await PlaceKlavyeAsync();

        await _sut.CancelAsync(id);

        Assert.Equal(OrderStatus.Cancelled, _orders.Store[id].Status);
        Assert.Contains($"cancelled:{id}", _notifications.Sent);
    }

    [Fact]
    public async Task CancelAsync_of_shipped_order_throws_domain_exception()
    {
        var id = await PlaceKlavyeAsync();
        await _sut.ShipAsync(id);

        await Assert.ThrowsAsync<DomainException>(() => _sut.CancelAsync(id));
        Assert.DoesNotContain($"cancelled:{id}", _notifications.Sent);
    }

    [Fact]
    public async Task GetAsync_of_missing_order_throws_not_found() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetAsync(Guid.NewGuid()));
}
