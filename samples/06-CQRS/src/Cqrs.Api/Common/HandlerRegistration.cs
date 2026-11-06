using System.Reflection;

namespace Cqrs.Api.Common;

public static class HandlerRegistration
{
    private static readonly Type[] HandlerInterfaces =
        [typeof(ICommandHandler<>), typeof(ICommandHandler<,>), typeof(IQueryHandler<,>)];

    // Assembly'deki tüm komut/sorgu handler'larını kapalı generic arayüzleriyle DI'a kaydeder.
    public static IServiceCollection AddCqrsHandlers(this IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.DefinedTypes.Where(t => t is { IsAbstract: false, IsInterface: false }))
        foreach (var itf in type.ImplementedInterfaces.Where(i =>
                     i.IsGenericType && HandlerInterfaces.Contains(i.GetGenericTypeDefinition())))
        {
            services.AddScoped(itf, type);
        }

        return services;
    }
}
