using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Application._Policy;

public class ServicePolicy<TRequest> : IAuthorizationPolicy<TRequest>
    where TRequest : IFunctionalRequest<object>
    {
        public Fin<Unit> Authorize(TRequest request, TrustedContext context) =>
            context.Role == "Service"
                ? Fin<Unit>.Succ(Unit.Default)
                : Fin<Unit>.Fail(Error.New("Unauthorized: Only services may use this function role required"));
    }

