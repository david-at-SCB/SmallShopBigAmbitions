using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Auth.Policy;

namespace SmallShopBigAmbitions.Application.Cart.AddItemsAndCheckout;

public class AddItemsAndCheckoutPolicy
    : IAuthorizationPolicy<AddItemsAndCheckoutCommand>
{
    public Fin<Unit> Authorize(AddItemsAndCheckoutCommand request, TrustedContext context) =>
        context.Role == "Admin" || context.Role == "Service"
            ? Fin<Unit>.Succ(Unit.Default)
            : Fin<Unit>.Fail(Error.New("Unauthorized to charge customer"));
}