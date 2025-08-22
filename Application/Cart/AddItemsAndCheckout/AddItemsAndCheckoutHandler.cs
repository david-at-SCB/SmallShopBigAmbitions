using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Monads.Traceable;
using SmallShopBigAmbitions.Monads.TraceableTransformer;

namespace SmallShopBigAmbitions.Application.Cart.AddItemsAndCheckout;

public class AddItemsAndCheckoutHandler : IFunctionalHandler<AddItemsAndCheckoutCommand, UserCheckoutResult>
{
    private readonly CartService _cartService;
    private readonly UserService _userService;
    private readonly ILogger<AddItemsAndCheckoutHandler> _logger;

    public AddItemsAndCheckoutHandler(
        CartService cartService,
        UserService userService,
        ILogger<AddItemsAndCheckoutHandler> logger)
    {
        _cartService = cartService;
        _userService = userService;
        _logger = logger;
    }

    public IO<Fin<UserCheckoutResult>> Handle(AddItemsAndCheckoutCommand request, TrustedContext context, CancellationToken ct)
    {
        var flow =
            from _ in TraceableTLifts.FromIO<Unit>( 
                AuthorizationGuards.RequireTrustedORThrow(context), 
                "RequireTrusted"
            )
            from cart in _cartService.GetCartForUser(request.UserId)
            from updated in _cartService.AddItems(cart, request.Items)
            from result in _userService.CheckoutExistingCart(updated, request.UserId)
            select result;

        return flow
            .WithSpanName("AddItemsAndCheckout")
            .WithLogging(_logger)
            .RunTraceableFin(ct);
    }
}