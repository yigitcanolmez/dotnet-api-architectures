namespace Clean.Domain.Common;

// [Domain Katmanı] Basit hata tipi: makinece okunabilir Code + insan için Message.
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}

// Basit Result tipi: "kural sağlanıyor mu?" sorusunu exception fırlatmadan cevaplar.
// Aggregate'ler önce Can...() ile Result üretir, komut metodu ise başarısızsa DomainException fırlatır.
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None) throw new ArgumentException("Başarılı sonuç hata içeremez.", nameof(error));
        if (!isSuccess && error == Error.None) throw new ArgumentException("Başarısız sonuç bir hata içermeli.", nameof(error));
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new(default, false, error);

    // Başarısızsa domain kuralı ihlali olarak fırlatır.
    public void ThrowIfFailure()
    {
        if (IsFailure) throw new DomainException(Error);
    }
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Başarısız bir sonucun değeri okunamaz.");
}
