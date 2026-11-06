using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Hexagonal.Adapters.Web;

public static class WebAdapterExtensions
{
    public static IServiceCollection AddWebAdapter(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<CoreExceptionHandler>();
        return services;
    }

    public static WebApplication UseWebAdapter(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.MapProductsEndpoints();
        app.MapOrdersEndpoints();
        return app;
    }
}
