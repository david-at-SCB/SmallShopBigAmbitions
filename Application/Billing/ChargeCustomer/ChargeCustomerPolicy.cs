using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Models;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application.Billing.ChargeCustomer;

public static class ChargeCustomerValidator
{
    public static Validation<Seq<Error>, Unit> Validate(ChargeCustomerCommand cmd, TrustedContext ctx)
    {
        var cartValidation = cmd.Cart.ValidateForCharge().Match(
            Succ: _ => Success<Seq<Error>, Unit>(unit),
            Fail: e => Fail<Seq<Error>, Unit>(Seq1(e)));

        return RuleCombiner.Apply(
            Rule.From("auth", () => ctx.IsAuthenticated, ErrorCodes.Auth_Unauthorized),
            Rule.From("role", () => ctx.Role == "Admin" || ctx.Role == "Service", ErrorCodes.Auth_InsufficientRole),
            cartValidation
        );
    }
}

public class ChargeCustomerPolicy : IAuthorizationPolicy<ChargeCustomerCommand>
{
    public Fin<Unit> Authorize(ChargeCustomerCommand request, TrustedContext context) =>
        ChargeCustomerValidator.Validate(request, context).ToFin();
}