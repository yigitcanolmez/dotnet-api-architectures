namespace Clean.Domain.Orders;

// Customer bu bounded context'te bir aggregate değil; sadece kimliğiyle temsil edilir.
public readonly record struct CustomerId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
