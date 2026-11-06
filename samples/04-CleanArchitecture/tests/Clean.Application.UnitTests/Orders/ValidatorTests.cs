using Clean.Application.Orders.Commands.CancelOrder;
using Clean.Application.Orders.Commands.PlaceOrder;

namespace Clean.Application.UnitTests.Orders;

// Validator'lar saf sınıflardır: DB/DI olmadan doğrudan test edilir.
public class ValidatorTests
{
    private readonly PlaceOrderCommandValidator _placeValidator = new();

    private static PlaceOrderItem Item(int quantity = 1) => new(Guid.NewGuid(), quantity);

    [Fact]
    public void PlaceOrder_Valid_Passes()
    {
        var result = _placeValidator.Validate(new PlaceOrderCommand(Guid.NewGuid(), [Item(2)]));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void PlaceOrder_EmptyCustomerAndItems_Fails()
    {
        var result = _placeValidator.Validate(new PlaceOrderCommand(Guid.Empty, []));

        Assert.Contains(result.Errors, e => e.PropertyName == "CustomerId");
        Assert.Contains(result.Errors, e => e.PropertyName == "Items");
    }

    [Fact]
    public void PlaceOrder_MoreThanTenItems_Fails()
    {
        var items = Enumerable.Range(0, 11).Select(_ => Item()).ToList();
        Assert.False(_placeValidator.Validate(new PlaceOrderCommand(Guid.NewGuid(), items)).IsValid);
    }

    [Fact]
    public void PlaceOrder_ZeroQuantity_Fails()
    {
        var result = _placeValidator.Validate(new PlaceOrderCommand(Guid.NewGuid(), [Item(0)]));
        Assert.Contains(result.Errors, e => e.PropertyName.EndsWith("Quantity"));
    }

    [Fact]
    public void PlaceOrder_DuplicateProduct_Fails()
    {
        var productId = Guid.NewGuid();
        var result = _placeValidator.Validate(new PlaceOrderCommand(Guid.NewGuid(), [new(productId, 1), new(productId, 2)]));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void CancelOrder_EmptyReason_Fails()
    {
        var result = new CancelOrderCommandValidator().Validate(new CancelOrderCommand(Guid.NewGuid(), ""));
        Assert.Contains(result.Errors, e => e.PropertyName == "Reason");
    }
}
