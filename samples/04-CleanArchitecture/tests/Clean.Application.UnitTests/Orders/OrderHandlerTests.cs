using Clean.Application.Abstractions;
using Clean.Application.Common.Exceptions;
using Clean.Application.Orders.Commands.CancelOrder;
using Clean.Application.Orders.Commands.PlaceOrder;
using Clean.Application.Orders.Commands.ShipOrder;
using Clean.Application.Orders.Queries.GetOrderById;
using Clean.Domain.Common;
using Clean.Domain.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Clean.Application.UnitTests.Orders;

public class OrderHandlerTests
{
    private static readonly Guid Keyboard = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Mouse = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Monitor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Customer = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static Task<Guid> PlaceAsync(TestHost host, params PlaceOrderItem[] items) =>
        host.RunAsync(sp => sp.GetRequiredService<ISender>().Send(new PlaceOrderCommand(Customer, items)));

    private static Task<int> StockOfAsync(TestHost host, Guid productId) =>
        host.RunAsync(async sp =>
        {
            var db = sp.GetRequiredService<IApplicationDbContext>();
            var id = new ProductId(productId);
            return await db.Products.AsNoTracking().Where(p => p.Id == id).Select(p => p.Stock).SingleAsync();
        });

    [Fact]
    public async Task PlaceOrder_PersistsOrderWithSnapshotAndTotal()
    {
        await using var host = await TestHost.CreateAsync();

        var id = await PlaceAsync(host, new PlaceOrderItem(Keyboard, 2), new PlaceOrderItem(Mouse, 1));

        var dto = await host.RunAsync(sp => sp.GetRequiredService<ISender>().Send(new GetOrderByIdQuery(id)));
        Assert.Equal("Placed", dto.Status);
        Assert.Equal(1850m, dto.Total);
        Assert.Equal("TRY", dto.Currency);
        Assert.Equal(TestHost.FixedNow, dto.CreatedAtUtc);
        Assert.Contains(dto.Lines, l => l.ProductName == "Klavye" && l.UnitPrice == 750m && l.Quantity == 2);
    }

    [Fact]
    public async Task PlaceOrder_OrderPlacedHandler_DecreasesStockInSameSave()
    {
        await using var host = await TestHost.CreateAsync();

        await PlaceAsync(host, new PlaceOrderItem(Keyboard, 3));

        Assert.Equal(7, await StockOfAsync(host, Keyboard));
    }

    [Fact]
    public async Task PlaceOrder_InsufficientStock_ThrowsAndPersistsNothing()
    {
        await using var host = await TestHost.CreateAsync();

        var ex = await Assert.ThrowsAsync<DomainException>(() => PlaceAsync(host, new PlaceOrderItem(Keyboard, 1), new PlaceOrderItem(Monitor, 6)));

        Assert.Equal("Product.InsufficientStock", ex.Code);
        Assert.Equal(10, await StockOfAsync(host, Keyboard)); // klavye stoğu da geri alınmış olmalı (atomik)
        var orderCount = await host.RunAsync(sp => sp.GetRequiredService<IApplicationDbContext>().Orders.CountAsync());
        Assert.Equal(0, orderCount);
    }

    [Fact]
    public async Task PlaceOrder_UnknownProduct_ThrowsNotFound()
    {
        await using var host = await TestHost.CreateAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => PlaceAsync(host, new PlaceOrderItem(Guid.NewGuid(), 1)));
    }

    [Fact]
    public async Task PlaceOrder_InvalidCommand_ThrowsValidationExceptionBeforeHandler()
    {
        await using var host = await TestHost.CreateAsync();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            host.RunAsync(sp => sp.GetRequiredService<ISender>().Send(new PlaceOrderCommand(Guid.Empty, []))));

        Assert.Contains("CustomerId", ex.Errors.Keys);
        Assert.Contains("Items", ex.Errors.Keys);
    }

    [Fact]
    public async Task ShipOrder_ThenCancel_ThrowsDomainException()
    {
        await using var host = await TestHost.CreateAsync();
        var id = await PlaceAsync(host, new PlaceOrderItem(Mouse, 1));

        await host.RunAsync(sp => sp.GetRequiredService<ISender>().Send(new ShipOrderCommand(id, "TRK-1")));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            host.RunAsync(sp => sp.GetRequiredService<ISender>().Send(new CancelOrderCommand(id, "vazgeçti"))));
        Assert.Equal("Order.ShippedCannotBeCancelled", ex.Code);
    }

    [Fact]
    public async Task CancelOrder_RestoresStock()
    {
        await using var host = await TestHost.CreateAsync();
        var id = await PlaceAsync(host, new PlaceOrderItem(Mouse, 5));
        Assert.Equal(15, await StockOfAsync(host, Mouse));

        await host.RunAsync(sp => sp.GetRequiredService<ISender>().Send(new CancelOrderCommand(id, "vazgeçti")));

        var dto = await host.RunAsync(sp => sp.GetRequiredService<ISender>().Send(new GetOrderByIdQuery(id)));
        Assert.Equal("Cancelled", dto.Status);
        Assert.Equal(20, await StockOfAsync(host, Mouse));
    }

    [Fact]
    public async Task ShipOrder_UnknownOrder_ThrowsNotFound()
    {
        await using var host = await TestHost.CreateAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            host.RunAsync(sp => sp.GetRequiredService<ISender>().Send(new ShipOrderCommand(Guid.NewGuid(), "TRK-1"))));
    }
}
