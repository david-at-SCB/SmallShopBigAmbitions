using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Auth;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application.HelloWorld;

public static class HelloWorldValidator
{
    public static Validation<Seq<Error>, Unit> Validate(HelloWorldRequest cmd, TrustedContext ctx) =>
        RuleCombiner.Apply(
            Rule.From("auth", () => ctx.IsAuthenticated, ErrorCodes.Auth_Unauthorized)
        );
}

public class HelloWorldPolicy: IAuthorizationPolicy<HelloWorldRequest> 
{
    public Fin<Unit> Authorize(HelloWorldRequest request, TrustedContext context) =>
        HelloWorldValidator.Validate(request, context).ToFin();
}
