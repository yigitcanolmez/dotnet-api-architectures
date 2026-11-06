namespace VerticalSlice.Api.Common;

// [Common] Gerçekten ortak olan tek şey: başarı/hata sonucu ve HTTP'ye çevrilmesi.
// Exception yerine Result kullanmak, beklenen iş hatalarını (409/404) akışın parçası yapar.
public enum ErrorType { Validation, NotFound, Conflict }

public sealed record Error(ErrorType Type, string Message)
{
    public static Error Validation(string message) => new(ErrorType.Validation, message);
    public static Error NotFound(string message) => new(ErrorType.NotFound, message);
    public static Error Conflict(string message) => new(ErrorType.Conflict, message);

    public IResult ToProblem() => Results.Problem(
        statusCode: Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        },
        title: Type.ToString(),
        detail: Message);
}

public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);
    public static Result Failure(Error error) => new(error);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;
    private Result(Error error) : base(error) { }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Başarısız sonucun değeri yok.");

    public static implicit operator Result<T>(T value) => new(value);
    public static implicit operator Result<T>(Error error) => new(error);
}
