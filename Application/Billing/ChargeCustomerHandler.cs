using MediatR;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;

namespace SmallShopBigAmbitions.Application.Billing
{
    public class ChargeCustomerHandler : IRequestHandler<ChargeCustomerCommand, Fin<BillingService.ChargeResult>>
    {
        private readonly ILogger<BillingService> _logger;

        public ChargeCustomerHandler(ILogger<BillingService> logger)
        {
            _logger = logger;
        }

        public async Task<Fin<BillingService.ChargeResult>> Handle(ChargeCustomerCommand request, CancellationToken ct)
        {
            var authResult = AuthorizationGuards.EnsureTrusted(request.Context);
            if (authResult.IsFail)
                return Fin<BillingService.ChargeResult>.Fail(authResult.IfFail("Unknown error")); // cant have a string, needs a Func<Error, Unit> ?

            var traceable = BillingService.ChargeCustomer(request.CartId, request.UserId, _logger);
            var result = await traceable.RunTraceable().AsTask();

            return IO<BillingService.ChargeResult>.LiftAsync(result);
        }
    }

}
