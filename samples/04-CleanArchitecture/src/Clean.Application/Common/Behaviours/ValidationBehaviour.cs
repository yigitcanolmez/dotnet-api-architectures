using FluentValidation;
using MediatR;
using ValidationException = Clean.Application.Common.Exceptions.ValidationException;

namespace Clean.Application.Common.Behaviours;

// [Application Katmanı – Cross-cutting] Handler çalışmadan ÖNCE isteğin tüm validator'larını koşturur.
// Girdi doğrulama (format, zorunlu alan) burada; iş kuralları (invariant) ise Domain'de.
public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next(cancellationToken);
    }
}
