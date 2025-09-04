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
        // Get cart then charge; errors inside services are surfaced as Fin failures
        var flow = _cartService
            .GetCartForUser(request.UserId) // there should already be a cart in the command right?
            .Bind(cart => _billingService
                .ChargeCustomer(cart)
                .RequireTrusted(context)
                .WithSpanName("ChargeCustomer")
                .WithLogging(_logger));

        // flow is TraceableT<Fin<ChargeResult>> so RunTraceable yields IO<Fin<ChargeResult>> (no double-Fin)
        return flow.RunTraceable(ct);
    }
}