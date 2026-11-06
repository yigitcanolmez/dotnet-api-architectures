using Cqrs.Api.Write.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Cqrs.Api.Common;

// Komutlar sadece void/ID döndüğü için iş hataları exception olarak taşınır ve burada ProblemDetails'e çevrilir.
public sealed class ProblemExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        ProblemDetails? problem = exception switch
        {
            CommandValidationException e => new HttpValidationProblemDetails(e.Errors) { Status = 400 },
            NotFoundException e => new ProblemDetails { Status = 404, Title = "NotFound", Detail = e.Message },
            DomainException e => new ProblemDetails { Status = 409, Title = "Conflict", Detail = e.Message },
            _ => null
        };
        if (problem is null) return false;

        http.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
