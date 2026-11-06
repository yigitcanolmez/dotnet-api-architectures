using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Onion.Application.Exceptions;
using Onion.Domain.Exceptions;

namespace Onion.Api.ErrorHandling;

// [UI halkası] Domain/Application exception'larını HTTP ProblemDetails'e çevirir.
// Domain HTTP status kodlarını bilmez; bu çeviri en dış halkanın sorumluluğu.
public class ExceptionToProblemDetailsHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        (int Status, string Title)? mapping = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Kayıt bulunamadı"),
            DomainException => (StatusCodes.Status409Conflict, "İş kuralı ihlali"),
            _ => null,
        };
        if (mapping is null) return false;

        context.Response.StatusCode = mapping.Value.Status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = mapping.Value.Status, Title = mapping.Value.Title, Detail = exception.Message },
        });
    }
}
