// [Gateway — Cross-cutting] Basit istek loglama + correlation id.
// Gateway her isteğe X-Correlation-Id ekler; YARP bu header'ı iç servislere de iletir.
using System.Diagnostics;

namespace Gateway.Api.Middleware;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public const string CorrelationHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(CorrelationHeader, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
            context.Request.Headers[CorrelationHeader] = correlationId;
        }
        context.Response.Headers[CorrelationHeader] = correlationId;

        var sw = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            logger.LogInformation("HTTP {Method} {Path}{Query} → {StatusCode} ({ElapsedMs} ms) CorrelationId={CorrelationId}",
                context.Request.Method, context.Request.Path, context.Request.QueryString, context.Response.StatusCode,
                sw.ElapsedMilliseconds, correlationId.ToString());
        }
    }
}
