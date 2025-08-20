using LanguageExt;
using MediatR;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;

namespace SmallShopBigAmbitions.Application.Billing
{
    public class ChargeCustomerHandler : IRequestHandler<ChargeCustomerCommand, Fin<ChargeResult>>
    {
        private readonly ILogger<BillingService> _logger;

        public ChargeCustomerHandler(ILogger<BillingService> logger)
        {
            _logger = logger;
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

            // does this run async?
            //var traceable = BillingService.ChargeCustomer(request.CartId, request.UserId, _logger);
            //var result = traceable.RunTraceable().Run();
            //return Task.FromResult(Fin<ChargeResult>.Succ(result));

            // this is lazy and async, no?
            var resultturn = BillingService
                .ChargeCustomer(request.CartId, request.UserId, _logger)
                .RunTraceable();

            return Task.FromResult(Fin<ChargeResult>.Succ(resultturn.Run()));

        }
    }

}
