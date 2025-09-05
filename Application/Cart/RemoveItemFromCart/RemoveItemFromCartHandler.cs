using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Application.Cart.RemoveItemFromCart;

public class RemoveItemFromCartHandler(
     ILogger<BillingService> logger,
    CartService cartService,
    UserService userService,
    ProductService productService)
    : IFunctionalHandler<RemoveItemFromCartCommand_Thin_Params, RemoveItemFromCartDTO>
{
    private readonly ILogger<BillingService> _logger = logger;
    private readonly CartService _cartService = cartService;
    private readonly UserService _userService = userService;
    private readonly ProductService _productService = productService;
    public IO<Fin<RemoveItemFromCartDTO>> Handle(RemoveItemFromCartCommand_Thin_Params request, TrustedContext context, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
