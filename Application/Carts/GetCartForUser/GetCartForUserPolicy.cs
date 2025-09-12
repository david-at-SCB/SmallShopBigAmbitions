using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Auth;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application.Cart.GetCartForUser;

public static class GetCartForUserValidator
{
    public static Validation<Seq<Error>, Unit> Validate(GetCartForUserQuery cmd, TrustedContext ctx) =>
        RuleCombiner.Apply(
            // Allow if authenticated OR caller id matches requested user id (anon cart) and not empty
            Rule.From("self_or_authed", () => ctx.IsAuthenticated || (ctx.CallerId != Guid.Empty && ctx.CallerId == cmd.UserId), ErrorCodes.Auth_Unauthorized)
        );
}

public class GetCartForUserPolicy : IAuthorizationPolicy<GetCartForUserQuery>
{
    public Fin<Unit> Authorize(GetCartForUserQuery request, TrustedContext context) =>
        GetCartForUserValidator.Validate(request, context).ToFin();
}