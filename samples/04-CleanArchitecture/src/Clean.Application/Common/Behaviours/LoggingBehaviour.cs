using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Clean.Application.Common.Behaviours;

// [Application Katmanı – Cross-cutting] MediatR pipeline'ının EN DIŞ halkası:
// her command/query için başlangıç, süre ve sonucu loglar. Handler'lar log kodu yazmak zorunda kalmaz.
public sealed class LoggingBehaviour<TRequest, TResponse>(ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        logger.LogInformation("➡️  {RequestName} başlıyor: {@Request}", name, request);
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next(cancellationToken);
            logger.LogInformation("✅ {RequestName} tamamlandı ({ElapsedMs} ms)", name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning("❌ {RequestName} başarısız ({ElapsedMs} ms): {Error}", name, sw.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }
}
