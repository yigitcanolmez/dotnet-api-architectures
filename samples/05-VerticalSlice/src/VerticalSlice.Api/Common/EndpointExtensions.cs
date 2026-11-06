using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace VerticalSlice.Api.Common;

// [Common] Assembly taraması (reflection): yeni bir dilim eklemek için Program.cs'e
// dokunmaya gerek yok. IEndpoint ve IHandler uygulayan her sınıf otomatik bulunur.
public static class EndpointExtensions
{
    public static IServiceCollection AddSlices(this IServiceCollection services, Assembly assembly)
    {
        var types = assembly.DefinedTypes
            .Where(t => t is { IsAbstract: false, IsInterface: false })
            .ToList();

        foreach (var handler in types.Where(t => typeof(IHandler).IsAssignableFrom(t)))
            services.AddScoped(handler);

        var endpoints = types
            .Where(t => typeof(IEndpoint).IsAssignableFrom(t))
            .Select(t => ServiceDescriptor.Transient(typeof(IEndpoint), t));
        services.TryAddEnumerable(endpoints);

        return services;
    }

    public static WebApplication MapEndpoints(this WebApplication app)
    {
        foreach (var endpoint in app.Services.GetServices<IEndpoint>())
            endpoint.MapEndpoint(app);

        return app;
    }
}
