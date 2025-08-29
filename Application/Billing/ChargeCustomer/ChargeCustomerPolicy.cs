using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using LanguageExt;

namespace SmallShopBigAmbitions.Application.Billing.ChargeCustomer;

public class ChargeCustomerPolicy : IAuthorizationPolicy<ChargeCustomerCommand>
{
    public Fin<Unit> Authorize(ChargeCustomerCommand request, TrustedContext context)
    {
        var cart = CartService.GetCartById(request.CartId);
        //var cart = request.Cart;

        // Role check
        if (context.Role != "Admin" && context.Role != "Service")
            return Fin<Unit>.Fail(Error.New("Unauthorized to charge customer"));

        // Cart must have items
        if (cart.Items.IsEmpty)
            return Fin<Unit>.Fail(Error.New("Cart is empty"));

        // Validate each item
        foreach (var item in cart.Items)
        {
            if (item.Value <= 0)
                return Fin<Unit>.Fail(Error.New($"Invalid quantity for item {item.Key.Title} with Id {item.Key.Id}"));

            if (item.Key.Price <= 0)
                return Fin<Unit>.Fail(Error.New($"Invalid price for item {item.Key.Title} with Id {item.Key.Id}"));
        }

        // Optional: Validate total amount (sum of quantity * unit price)
        decimal total = 0m;
        foreach (var (Product, Quantity) in cart.Items)
        {
            total += Product.Price * Quantity;
        }

        if (total <= 0)
            return Fin<Unit>.Fail(Error.New("Total cart amount must be greater than zero"));

        return Fin<Unit>.Succ(Unit.Default);
    }
}
