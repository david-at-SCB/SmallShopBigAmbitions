using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.Monads.TraceableTransformer.Extensions.BaseLinq;

namespace SmallShopBigAmbitions.Application.Billing.CheckoutUser;

public class CheckoutUserHandler : IFunctionalHandler<CheckoutUserCommand, CheckoutUserResultDTO>
{
    private readonly UserService _userService;
    private readonly CartService _cartService;
    private readonly BillingService _billingService;

    public CheckoutUserHandler(UserService userService, CartService cartService, BillingService billingService)
    {
        _userService = userService;
        _cartService = cartService;
        _billingService = billingService;
    }

    public IO<Fin<CheckoutUserResultDTO>> Handle(CheckoutUserCommand request, TrustedContext context, CancellationToken ct)
    {
        var flow =
            from cartFin in _cartService.GetCartForUser(request.UserId)
                .WithSpanName("cart.fetch_for_checkout")
            from checkoutFin in cartFin.Match(
                Succ: cart => _billingService
                    .CheckoutCustomerCart(cart)
                    .RequireTrusted(context)
                    .WithSpanName("CheckoutUser")
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