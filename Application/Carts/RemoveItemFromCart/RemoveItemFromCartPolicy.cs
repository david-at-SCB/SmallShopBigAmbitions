using LanguageExt;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Application._Policy;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application.Cart.RemoveItemFromCart;

public static class RemoveItemFromCartValidator
{
    public static Validation<Seq<Error>, Unit> Validate(RemoveItemFromCartCommand cmd, TrustedContext ctx) =>
        RuleCombiner.Apply(
            Rule.From("auth", () => ctx.IsAuthenticated, ErrorCodes.Auth_Unauthorized)
        );
}

public sealed class RemoveItemFromCartPolicy : IAuthorizationPolicy<RemoveItemFromCartCommand>
{
    public Fin<Unit> Authorize(RemoveItemFromCartCommand request, TrustedContext context) =>
        RemoveItemFromCartValidator.Validate(request, context).ToFin();
}
