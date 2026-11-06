namespace Modern.Api.Domain;

// [Domain] Value object: tutar artık "1.500,00" gibi bir string değil, para birimiyle birlikte bir değer.
public sealed record Money(decimal Amount, string Currency)
{
    public static Money Zero(string currency) => new(0m, currency);
    public static Money Try(decimal amount) => new(amount, "TRY");

    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency) throw new DomainException($"Para birimleri uyuşmuyor: {a.Currency} / {b.Currency}");
        return new(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator *(Money a, int factor) => new(a.Amount * factor, a.Currency);

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
