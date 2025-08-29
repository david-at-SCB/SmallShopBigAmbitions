using SmallShopBigAmbitions.Application.Billing.CheckoutUser;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using LanguageExt;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application.Cart.AddItemsAndCheckout;

public class AddItemsToCartHandler(
    CartService cartService,
    ILogger<AddItemsToCartHandler> logger) : IFunctionalHandler<AddItemsToCartCommand, CheckoutUserResultDTO>
{
    private readonly CartService _CartService = cartService;
    private readonly ILogger<AddItemsToCartHandler> _Logger = logger;

    public IO<Fin<CheckoutUserResultDTO>> Handle(AddItemsToCartCommand request, TrustedContext context, CancellationToken ct)
    {
        // Get the current cart for the user, requiring a trusted context first
        var trace =
            from cart in _CartService
                .GetCartForUser(request.CustomerId)
                .RequireTrusted(context, "RequireTrusted")
                .WithSpanName("cart.get_for_user")
                .WithLogging(_Logger)
            // Merge incoming items into the cart
            from updated in _CartService
                .AddItems(cart, request.Items)
                .WithSpanName("cart.add_items")
                .WithLogging(_Logger)
            // Project to a simple result DTO (no charge performed here)
            select new CheckoutUserResultDTO(
                CustomerId: request.CustomerId,
                Cart: updated,
                Message: "Items added to cart.",
                Charged: Fin<ChargeResult>.Fail("Not charged")
            );

        return trace
            .WithSpanName("AddItemsToCart")
            .WithLogging(_Logger)
            .RunTraceableFin(ct);
    }
}