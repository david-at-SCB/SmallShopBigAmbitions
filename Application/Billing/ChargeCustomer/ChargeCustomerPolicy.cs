using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Auth.Policy;

namespace SmallShopBigAmbitions.Application.Billing.ChargeCustomer;

public class ChargeCustomerPolicy : IAuthorizationPolicy<ChargeCustomerCommand>
{
    public Fin<Unit> Authorize(ChargeCustomerCommand request, TrustedContext context) =>
        context.Role == "Admin" || context.Role == "Service"
            ? Fin<Unit>.Succ(Unit.Default)
            : Fin<Unit>.Fail(Error.New("Unauthorized to charge customer"));
}
