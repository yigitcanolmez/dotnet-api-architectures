using EventSourcing.Api.Domain;
using EventSourcing.Api.EventStore;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EventSourcing.Api.Common;

// Domain kuralı ihlali ve optimistic concurrency çakışması → 409 ProblemDetails.
public sealed class ProblemExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        ProblemDetails? problem = exception switch
        {
            DomainException e => new ProblemDetails { Status = 409, Title = "Conflict", Detail = e.Message },
            ConcurrencyException e => new ProblemDetails
            {
                Status = 409, Title = "Concurrency", Detail = e.Message,
                Extensions = { ["expectedVersion"] = e.ExpectedVersion, ["actualVersion"] = e.ActualVersion }
            },
            _ => null
        };
        if (problem is null) return false;

        http.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http, ProblemDetails = problem, Exception = exception
        });
    }
}
