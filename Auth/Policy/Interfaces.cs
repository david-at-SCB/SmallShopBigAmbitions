using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Auth.Policy;

public interface IAuthorizationPolicy<TRequest>
{
    Fin<Unit> Authorize(TRequest request, TrustedContext context);
}
