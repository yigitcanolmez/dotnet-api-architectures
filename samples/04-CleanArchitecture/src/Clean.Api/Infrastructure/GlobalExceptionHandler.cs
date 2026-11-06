using Clean.Application.Common.Exceptions;
using Clean.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Clean.Api.Infrastructure;

// [Api Katmanı] İç halkaların exception'larını HTTP diline (ProblemDetails, RFC 9457) çevirir.
//   ValidationException (Application) → 400
//   NotFoundException   (Application) → 404
//   DomainException     (Domain)      → 409 (iş kuralı ihlali: istek geçerli ama kaynağın durumu buna izin vermiyor)
// Not: Bu klasör adı "Infrastructure" olsa da Clean.Infrastructure projesiyle ilgisi yok; Web'e özgü altyapıdır.
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException ve => new HttpValidationProblemDetails(ve.Errors.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Doğrulama hatası",
                Detail = ve.Message
            },
            BadHttpRequestException bad => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Geçersiz istek",
                Detail = bad.Message
            },
            NotFoundException nf => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Kaynak bulunamadı",
                Detail = nf.Message
            },
            DomainException de => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "İş kuralı ihlali",
                Detail = de.Message,
                Extensions = { ["code"] = de.Code }
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Beklenmeyen hata"
            }
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "İşlenmeyen hata");

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
