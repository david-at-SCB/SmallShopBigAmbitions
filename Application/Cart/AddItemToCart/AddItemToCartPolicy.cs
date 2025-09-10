using LanguageExt;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.Application._Policy;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application.Cart.AddItemToCart;

public class AddItemToCartPolicy : IAuthorizationPolicy<AddItemToCartCommand>
{
    public Fin<Unit> Authorize(AddItemToCartCommand request, TrustedContext context) =>
        AddItemToCartValidator.Validate(request, context, currentDistinctLines: 0).ToFin();

    public static TraceableT<Fin<Unit>> Evaluate(AddItemToCartCommand request, TrustedContext ctx, int currentDistinctLines) =>
        PolicyRunner
            .Run(
                policyName: "policy.add_item_to_cart",
                validate: () => AddItemToCartValidator.Validate(request, ctx, currentDistinctLines))
            .WithAttributes(fin => fin.Match(
                Succ: _ => new[]
                {
                    new KeyValuePair<string, object>("quantity", request.Quantity.Value),
                    new KeyValuePair<string, object>("current.lines", currentDistinctLines)
                },
                Fail: e => new[] { new KeyValuePair<string, object>("error.raw", e.Message) }))
            .WithErrorCodeTags();
}
