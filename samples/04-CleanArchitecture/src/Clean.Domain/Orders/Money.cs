using Clean.Domain.Common;

namespace Clean.Domain.Orders;

// [Domain Katmanı] Value Object: kimliği yok, değerleriyle eşit, değişmez (immutable).
// 100 TRY ile 100 TRY "aynı"dır; hangisi olduğunun önemi yoktur. Para birimi kuralları burada yaşar.
public sealed record Money
{
    public const string DefaultCurrency = "TRY";

    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new DomainException(new Error("Money.Negative", "Tutar negatif olamaz."));
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new DomainException(new Error("Money.InvalidCurrency", "Para birimi 3 harfli ISO kodu olmalı (ör. TRY)."));

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    public static Money operator +(Money a, Money b)
    {
        EnsureSameCurrency(a, b);
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator *(Money money, int multiplier) => new(money.Amount * multiplier, money.Currency);

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (a.Currency != b.Currency)
            throw new DomainException(new Error("Money.CurrencyMismatch",
                $"Para birimleri uyuşmuyor: {a.Currency} ve {b.Currency}."));
    }

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
