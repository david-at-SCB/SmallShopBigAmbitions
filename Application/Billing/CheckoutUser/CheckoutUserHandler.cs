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

    public CheckoutUserHandler(UserService userService, CartService cartService, BillingService billingService, ILogger<CheckoutUserHandler> logger)
    {
        _userService = userService;
        _cartService = cartService;
        _billingService = billingService;
        _logger = logger;
    }

    public IO<Fin<CheckoutUserResultDTO>> Handle(CheckoutUserCommand request, TrustedContext context, CancellationToken ct)
    {
        var flow =
            from cartFin in _cartService.GetCartForUser(request.UserId)
                .WithSpanName("cart.fetch_for_checkout")
                .WithLogging(_logger)
            from checkoutFin in cartFin.Match(
                Succ: cart => _billingService
                    .CheckoutCustomerCart(cart)
                    .RequireTrusted(context)
                    .WithSpanName("CheckoutUser")
                    .WithLogging(_logger)
                    .Map(dto => Fin<CheckoutUserResultDTO>.Succ(dto)),
                Fail: e => TraceableTLifts.FromFin(
                    Fin<CheckoutUserResultDTO>.Fail(e),
                    "checkout.skip",
                    _ => new[] { new KeyValuePair<string, object>("error", e.Message) })
            )
            select checkoutFin;

        return flow.RunTraceable(ct);
    }
}