using Microsoft.Extensions.DependencyInjection;

namespace Hexagonal.Adapters.Messaging;

public static class MessagingAdapterExtensions
{
    public static IServiceCollection AddMessagingAdapter(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryOrderQueue>();
        services.AddHostedService<PlaceOrderConsumer>();
        return services;
    }
}
