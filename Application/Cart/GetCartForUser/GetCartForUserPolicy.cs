using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Auth.Policy;

namespace SmallShopBigAmbitions.Application.Cart.GetCartForUser;

public class GetCartForUserPolicy 
    : IAuthorizationPolicy<GetCartForUserQuery>
{
    public Fin<Unit> Authorize(GetCartForUserQuery request, TrustedContext context) =>
        context.Role == "Service"
            ? Fin<Unit>.Succ(Unit.Default)
            : Fin<Unit>.Fail(Error.New("Unauthorized: Only services may use this function role required"));
}