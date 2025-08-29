using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Application._Policy;

public interface IAuthorizationPolicy<TRequest>
{
    Fin<Unit> Authorize(TRequest request, TrustedContext context);
}
