using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.Monads.TraceableTransformer.Extensions.BaseLinq;

namespace SmallShopBigAmbitions.Application.Billing.ChargeCustomer;

public class ChargeCustomerHandler(BillingService billingService, CartService cartService) : IFunctionalHandler<ChargeCustomerCommand, ChargeResult>
{
    private readonly BillingService _billingService = billingService;
    private readonly CartService _cartService = cartService;

    public IO<Fin<ChargeResult>> Handle(ChargeCustomerCommand request, TrustedContext context, CancellationToken ct)
    {
        var flow =
            from cartFin in _cartService.GetCartForUser(request.UserId)
            from chargeFin in cartFin.Match(
                Succ: cart => _billingService
                    .ChargeCustomer(cart)
                    .RequireTrusted(context)
                    .WithSpanName("ChargeCustomer"),
                Fail: e => TraceableTLifts.FromFin(Fin<ChargeResult>.Fail(e), "charge.skip", _ => [])
            )
            select chargeFin;

        return flow.RunTraceable(ct);
    }
}