using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Billing.ChargeCustomer;

public class ChargeCustomerHandler : IFunctionalHandler<ChargeCustomerCommand, ChargeResult>
{
    private readonly ILogger<BillingService> _logger;
    private readonly BillingService _billingService;

    public ChargeCustomerHandler(ILogger<BillingService> logger, BillingService billingService)
    {
        _logger = logger;
        _billingService = billingService;
    }

    public IO<Fin<ChargeResult>> Handle(ChargeCustomerCommand request, TrustedContext context, CancellationToken ct) =>
        from _ in IO.lift(() => AuthorizationGuards.EnsureTrusted(context)) // request.Context doesnt exist anymore?
        from result in _billingService.ChargeCustomer(request.CartId, request.UserId).RunTraceable(ct)
        select result;
}