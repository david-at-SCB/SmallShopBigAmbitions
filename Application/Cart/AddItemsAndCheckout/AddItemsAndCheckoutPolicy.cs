using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Auth;

namespace SmallShopBigAmbitions.Application.Cart.AddItemsAndCheckout;

public class AddItemsAndCheckoutPolicy
    : IAuthorizationPolicy<AddItemsToCartCommand>
{
    public Fin<Unit> Authorize(AddItemsToCartCommand request, TrustedContext context) =>
        context.Role == "Admin" || context.Role == "Service"
            ? Fin<Unit>.Succ(Unit.Default)
            : Fin<Unit>.Fail(Error.New("Unauthorized to charge customer"));
}