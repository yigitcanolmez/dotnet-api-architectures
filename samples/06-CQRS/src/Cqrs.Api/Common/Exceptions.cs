namespace Cqrs.Api.Common;

public sealed class NotFoundException(string message) : Exception(message);

public sealed class CommandValidationException(IDictionary<string, string[]> errors)
    : Exception("Komut geçersiz.")
{
    public IDictionary<string, string[]> Errors { get; } = errors;
}
