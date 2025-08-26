using SmallShopBigAmbitions.Application._Policy;

namespace SmallShopBigAmbitions.FunctionalDispatcher.DI;

public static class FunctionalRegistration
{
    public static void AddFunctionalHandlerWithPolicy<TRequest, TResponse, THandler, TPolicy>(this IServiceCollection services)
        where TRequest : IFunctionalRequest<TResponse>
        where THandler : class, IFunctionalHandler<TRequest, TResponse>
        where TPolicy : class, IAuthorizationPolicy<TRequest>
    {
        services.AddScoped<IFunctionalHandler<TRequest, TResponse>, THandler>();
        services.AddScoped<IAuthorizationPolicy<TRequest>, TPolicy>();
    }
}
