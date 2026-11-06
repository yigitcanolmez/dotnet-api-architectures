namespace Clean.Domain.Orders;

// [Domain Katmanı] Strongly-typed ID: OrderId yerine yanlışlıkla CustomerId geçirmeyi derleyici engeller.
// readonly record struct → değer eşitliği, değişmezlik, sıfır heap maliyeti.
public readonly record struct OrderId(Guid Value)
{
    public static OrderId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}
