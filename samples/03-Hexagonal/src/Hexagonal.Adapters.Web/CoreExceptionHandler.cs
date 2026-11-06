using Hexagonal.Core.Domain;
using Hexagonal.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hexagonal.Adapters.Web;

// [Driving Adapter: HTTP] Core'un exception'larını HTTP diline çevirmek bu adapter'ın işi.
// (Messaging adapter aynı exception'ları log + "mesajı reddet" olarak yorumlar.)
public class CoreExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        (int Status, string Title)? mapping = exception switch
        {
            OrderNotFoundException => (StatusCodes.Status404NotFound, "Kayıt bulunamadı"),
            PaymentDeclinedException => (StatusCodes.Status402PaymentRequired, "Ödeme reddedildi"),
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
