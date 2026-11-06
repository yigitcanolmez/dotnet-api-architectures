using FluentValidation;

namespace Clean.Application.Orders.Commands.PlaceOrder;

// Girdi doğrulama: hızlı geri bildirim için. Domain aynı kuralları (1..10 kalem, miktar > 0) yine korur;
// validator "kullanıcı dostu 400", domain ise "asla bozulmayan son savunma hattı"dır.
public sealed class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("Müşteri kimliği zorunludur.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Sipariş en az 1 kalem içermeli.")
            .Must(items => items is null || items.Count <= 10).WithMessage("Sipariş en fazla 10 kalem içerebilir.")
            .Must(items => items is null || items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("Aynı ürün birden fazla satırda olamaz; miktarı artırın.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).NotEmpty().WithMessage("Ürün kimliği zorunludur.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Miktar 0'dan büyük olmalı.");
        });
    }
}
