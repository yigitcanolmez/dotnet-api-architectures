using FluentValidation.Results;

namespace Clean.Application.Common.Exceptions;

// FluentValidation hatalarını alan adına göre gruplar; API katmanında 400 ValidationProblem'e çevrilir.
public sealed class ValidationException : Exception
{
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("Bir veya daha fazla doğrulama hatası oluştu.")
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.Distinct().ToArray());
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
