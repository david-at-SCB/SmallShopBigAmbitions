using LanguageExt;
using MediatR;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;

namespace SmallShopBigAmbitions.Application.Billing
{
    public class ChargeCustomerHandler : IRequestHandler<ChargeCustomerCommand, Fin<ChargeResult>>
    {
        private readonly ILogger<BillingService> _logger;
        private readonly BillingService _billingService;

        public ChargeCustomerHandler(ILogger<BillingService> logger, BillingService billingService)
        {
            _logger = logger;
            _billingService = billingService;
        }

        public Task<Fin<ChargeResult>> Handle(ChargeCustomerCommand request, CancellationToken ct)
        {
            var authResult = AuthorizationGuards.EnsureTrusted(request.Context);
            if (authResult.IsFail)
            {
                var err = authResult.Match(
                    Succ: _ => Error.New("Unauthorized"),
                    Fail: e => e);            
                return Task.FromResult(Fin<ChargeResult>.Fail(err));
            }

            try
            {
                var result = _billingService
                    .ChargeCustomer(request.CartId, request.UserId)
                    .RunTraceable(ct)
                    .Run();

                return Task.FromResult(Fin<ChargeResult>.Succ(result));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Fin<ChargeResult>.Fail(Error.New(ex.Message)));
            }
        }
    }
}
