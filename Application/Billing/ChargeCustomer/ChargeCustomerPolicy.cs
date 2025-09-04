using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;

namespace SmallShopBigAmbitions.Application.Billing.ChargeCustomer;

public class ChargeCustomerPolicy : IAuthorizationPolicy<ChargeCustomerCommand>
{
    public Fin<Unit> Authorize(ChargeCustomerCommand request, TrustedContext context)
    {
        var cart = CartService.GetCartByCartId(request.CartId);
        //var cart = request.Cart;

        // Role check
        if (context.Role != "Admin" && context.Role != "Service")
            return Fin<Unit>.Fail(Error.New("Unauthorized to charge customer"));

        // Declarative cart validation (accumulates errors internally; converted to Fin here)
        return cart.ValidateForCharge();
    }
}