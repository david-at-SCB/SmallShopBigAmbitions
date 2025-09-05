using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Billing.ChargeCustomer;

public class ChargeCustomerPolicy : IAuthorizationPolicy<ChargeCustomerCommand>
{
    // Synchronous authorization + validation
    public Fin<Unit> Authorize(ChargeCustomerCommand request, TrustedContext context)
    {
        // Must be authenticated
        if (!context.IsAuthenticated)
            return Fin<Unit>.Fail(Error.New("Unauthorized: caller not authenticated"));

        // Role check (expand as needed)
        if (context.Role != "Admin" && context.Role != "Service")
            return Fin<Unit>.Fail(Error.New("Unauthorized: insufficient role"));

        // Use the cart provided in the command (no I/O here)
        var cart = request.Cart;

        // Domain validation (returns Fin<Unit>)
        return cart.ValidateForCharge();
    }
}