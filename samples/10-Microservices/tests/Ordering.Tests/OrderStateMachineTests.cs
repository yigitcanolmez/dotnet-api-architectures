// [Test] Saga orkestratörü RabbitMQ OLMADAN, MassTransit'in in-memory test harness'ı ile test edilir.
// Katılımcı servislerin (Inventory/Payment) cevaplarını biz Publish ederek simüle ediyoruz.
using Contracts.Commands;
using Contracts.Events;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Api.Saga;

namespace Ordering.Tests;

public class OrderStateMachineTests : IAsyncLifetime
{
    private static readonly Guid Keyboard = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;
    private ISagaStateMachineTestHarness<OrderStateMachine, OrderState> _sagaHarness = null!;

    public async Task InitializeAsync()
    {
        _provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
                x.AddSagaStateMachine<OrderStateMachine, OrderState>().InMemoryRepository())
            .BuildServiceProvider(true);

        _harness = _provider.GetRequiredService<ITestHarness>();
        _harness.TestTimeout = TimeSpan.FromSeconds(5);
        await _harness.Start();
        _sagaHarness = _harness.GetSagaStateMachineHarness<OrderStateMachine, OrderState>();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    private async Task<Guid> SubmitOrder(decimal unitPrice = 750m, int quantity = 1)
    {
        var orderId = NewId.NextGuid();
        await _harness.Bus.Publish(new OrderSubmitted(orderId, Guid.NewGuid(), unitPrice * quantity,
            [new OrderItem(Keyboard, quantity, unitPrice)], DateTime.UtcNow));
        Assert.True(await _sagaHarness.Exists(orderId, x => x.AwaitingStock) is not null, "Saga AwaitingStock'a geçmeliydi");
        return orderId;
    }

    [Fact]
    public async Task OrderSubmitted_sends_ReserveStock_and_waits_for_stock()
    {
        var orderId = await SubmitOrder(quantity: 2);

        Assert.True(await _harness.Sent.Any<ReserveStock>(x =>
            x.Context.Message.OrderId == orderId &&
            x.Context.Message.Items.Single().Quantity == 2 &&
            x.Context.DestinationAddress!.AbsolutePath.EndsWith(QueueNames.ReserveStock)));
    }

    [Fact]
    public async Task Happy_path_reserves_stock_processes_payment_and_confirms_order()
    {
        var orderId = await SubmitOrder();

        await _harness.Bus.Publish(new StockReserved(orderId));
        Assert.NotNull(await _sagaHarness.Exists(orderId, x => x.AwaitingPayment));
        Assert.True(await _harness.Sent.Any<ProcessPayment>(x => x.Context.Message.OrderId == orderId && x.Context.Message.Amount == 750m));

        var paymentId = Guid.NewGuid();
        await _harness.Bus.Publish(new PaymentCompleted(orderId, paymentId, 750m));
        Assert.NotNull(await _sagaHarness.Exists(orderId, x => x.Completed));
        Assert.True(await _harness.Published.Any<OrderConfirmed>(x => x.Context.Message.OrderId == orderId));

        var saga = _sagaHarness.Sagas.Contains(orderId);
        Assert.Equal(paymentId, saga!.PaymentId);
        Assert.False(await _harness.Sent.Any<ReleaseStock>(x => x.Context.Message.OrderId == orderId));
    }

    [Fact]
    public async Task Payment_failure_compensates_by_sending_ReleaseStock_and_rejects_order()
    {
        var orderId = await SubmitOrder(unitPrice: 6500m, quantity: 2);
        await _harness.Bus.Publish(new StockReserved(orderId));
        Assert.NotNull(await _sagaHarness.Exists(orderId, x => x.AwaitingPayment));

        await _harness.Bus.Publish(new PaymentFailed(orderId, "Limit aşıldı"));

        Assert.NotNull(await _sagaHarness.Exists(orderId, x => x.Failed));
        Assert.True(await _harness.Sent.Any<ReleaseStock>(x =>
            x.Context.Message.OrderId == orderId &&
            x.Context.DestinationAddress!.AbsolutePath.EndsWith(QueueNames.ReleaseStock)), "Compensation (ReleaseStock) gönderilmeliydi");
        Assert.True(await _harness.Published.Any<OrderRejected>(x => x.Context.Message.OrderId == orderId));
        Assert.False(await _harness.Published.Any<OrderConfirmed>(x => x.Context.Message.OrderId == orderId));
        Assert.Equal("Limit aşıldı", _sagaHarness.Sagas.Contains(orderId)!.FailureReason);
    }

    [Fact]
    public async Task Stock_failure_rejects_order_without_payment_or_compensation()
    {
        var orderId = await SubmitOrder(quantity: 100);

        await _harness.Bus.Publish(new StockReservationFailed(orderId, "Stok yetersiz"));

        Assert.NotNull(await _sagaHarness.Exists(orderId, x => x.Failed));
        Assert.True(await _harness.Published.Any<OrderRejected>(x => x.Context.Message.OrderId == orderId));
        Assert.False(await _harness.Sent.Any<ProcessPayment>(x => x.Context.Message.OrderId == orderId));
        Assert.False(await _harness.Sent.Any<ReleaseStock>(x => x.Context.Message.OrderId == orderId));
    }

    [Fact]
    public async Task Duplicate_PaymentCompleted_after_completion_is_ignored()
    {
        var orderId = await SubmitOrder();
        await _harness.Bus.Publish(new StockReserved(orderId));
        Assert.NotNull(await _sagaHarness.Exists(orderId, x => x.AwaitingPayment));
        await _harness.Bus.Publish(new PaymentCompleted(orderId, Guid.NewGuid(), 750m));
        Assert.NotNull(await _sagaHarness.Exists(orderId, x => x.Completed));

        await _harness.Bus.Publish(new PaymentCompleted(orderId, Guid.NewGuid(), 750m)); // at-least-once tekrarı

        Assert.True(await _sagaHarness.Consumed.Any<PaymentCompleted>(x => x.Context.Message.OrderId == orderId));
        Assert.False(await _harness.Published.Any<Fault<PaymentCompleted>>());
        Assert.NotNull(await _sagaHarness.Exists(orderId, x => x.Completed));
    }
}
