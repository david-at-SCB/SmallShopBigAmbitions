using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;

namespace SmallShopBigAmbitions.Application.Billing.ChargeCustomer;

public class ChargeCustomerHandler : IFunctionalHandler<ChargeCustomerCommand, ChargeResult>
{
    private readonly ILogger<BillingService> _logger;
    private readonly BillingService _billingService;
    private readonly CartService _cartService;

    public ChargeCustomerHandler(ILogger<BillingService> logger, BillingService billingService, CartService cartService)
    {
        _logger = logger;
        _billingService = billingService;
        _cartService = cartService;
    }

    public IO<Fin<ChargeResult>> Handle(ChargeCustomerCommand request, TrustedContext context, CancellationToken ct)
    {
        var flow =
            from cartFin in _cartService.GetCartForUser(request.UserId)
            from chargeFin in cartFin.Match(
                Succ: cart => _billingService
                    .ChargeCustomer(cart)
                    .RequireTrusted(context)
                    .WithSpanName("ChargeCustomer")
                    .WithLogging(_logger),
                Fail: e => TraceableTLifts.FromFin(Fin<ChargeResult>.Fail(e), "charge.skip", _ => [])
            )
            select chargeFin;

        return flow.RunTraceable(ct);
    }
}