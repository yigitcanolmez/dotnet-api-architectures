using Layered.Business.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Layered.Api.Middleware;

// [Presentation Katmanı] Business exception'larını HTTP ProblemDetails yanıtlarına çevirir.
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is NotFoundException or BusinessException)
        {
            var (status, title) = ex switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, "Kayıt bulunamadı"),
                _ => (StatusCodes.Status409Conflict, "İş kuralı ihlali"),
            };

            logger.LogInformation("İstek {Status} ile sonuçlandı: {Message}", status, ex.Message);
            context.Response.StatusCode = status;
            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = ex.Message },
            });
        }
    }
}
