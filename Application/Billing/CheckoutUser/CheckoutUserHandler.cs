using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;

namespace SmallShopBigAmbitions.Application.Billing.CheckoutUser;

public class CheckoutUserHandler : IFunctionalHandler<CheckoutUserCommand, CheckoutUserResultDTO>
{
    private readonly UserService _userService;
    private readonly CartService _cartService;
    private readonly BillingService _billingService;
    private readonly ILogger<CheckoutUserHandler> _logger;

    public CheckoutUserHandler(UserService userService, CartService cartService,BillingService blnsrvc, ILogger<CheckoutUserHandler> logger)
    {
        _userService = userService;
        _cartService = cartService;
        _billingService = blnsrvc;
        _logger = logger;
    }

    public IO<Fin<CheckoutUserResultDTO>> Handle(CheckoutUserCommand request, TrustedContext context, CancellationToken ct)
    {
        // Fetch the user's cart, then checkout that existing cart
        var flow =
            from cart in _cartService.GetCartForUser(request.UserId)
            from result in _billingService.CheckoutCustomerCart(cart)
                                .RequireTrusted(context)
                                .WithSpanName("CheckoutUser")
                                .WithLogging(_logger)
            select result;

        return flow.RunTraceableFin(ct);
    }
}