using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Application._Policy;

namespace SmallShopBigAmbitions.Application.Carts.AddItemToCart;

public static class AddItemToCartValidator
{
    private const int MaxQuantityPerLine = 1000;
    private const int MaxDistinctLines = 100;

    public static Validation<Seq<Error>, Unit> Validate(AddItemToCartCommand cmd, TrustedContext ctx, int currentDistinctLines) =>
        RuleCombiner.Apply(
            Rule.From("user_present", () => ctx.IsAuthenticated || cmd.UserId != Guid.Empty, ErrorCodes.Auth_Unauthorized),
            Rule.From("qty_positive", () => cmd.Quantity.Value > 0, ErrorCodes.Cart_Add_QuantityNonPositive),
            Rule.From("qty_limit", () => cmd.Quantity.Value <= MaxQuantityPerLine, ErrorCodes.Cart_Add_QuantityTooLarge),
            Rule.From("currency_present", () => !string.IsNullOrWhiteSpace(cmd.PriceRef.Currency), ErrorCodes.Cart_Add_CurrencyMissing),
            Rule.From("distinct_lines", () => currentDistinctLines < MaxDistinctLines, ErrorCodes.Cart_Add_TooManyLines)
        );
}
