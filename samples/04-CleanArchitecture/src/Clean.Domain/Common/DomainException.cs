namespace Clean.Domain.Common;

// [Domain Katmanı] İş kuralı (invariant) ihlali. API katmanı bunu 409 Conflict'e çevirir.
public sealed class DomainException(Error error) : Exception(error.Message)
{
    public Error Error { get; } = error;

    public string Code => Error.Code;
}
