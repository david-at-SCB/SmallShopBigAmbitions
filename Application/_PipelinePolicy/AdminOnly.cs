using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Application._Policy;

public class AdminOnlyPolicy<TRequest> : IAuthorizationPolicy<TRequest>
    where TRequest : IFunctionalRequest<object>
{
    public Fin<Unit> Authorize(TRequest request, TrustedContext context) =>
        context.Role == "Admin"
            ? Fin<Unit>.Succ(Unit.Default)
            : Fin<Unit>.Fail(Error.New("Unauthorized: Admin role required"));
}
