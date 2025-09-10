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
            Rule.From("role_service", () => ctx.Role == "Service", ErrorCodes.Auth_InsufficientRole)
        );
}

public class GetCartForUserPolicy : IAuthorizationPolicy<GetCartForUserQuery>
{
    public Fin<Unit> Authorize(GetCartForUserQuery request, TrustedContext context) =>
        GetCartForUserValidator.Validate(request, context).ToFin();
}