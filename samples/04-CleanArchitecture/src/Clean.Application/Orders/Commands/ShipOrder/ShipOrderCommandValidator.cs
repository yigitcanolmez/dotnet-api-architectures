using FluentValidation;

namespace Clean.Application.Orders.Commands.ShipOrder;

public sealed class ShipOrderCommandValidator : AbstractValidator<ShipOrderCommand>
{
    public ShipOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.TrackingNumber).NotEmpty().MaximumLength(50);
    }
}
