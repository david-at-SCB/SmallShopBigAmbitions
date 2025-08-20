using SmallShopBigAmbitions.Monads.TraceableTransformer;

namespace SmallShopBigAmbitions.Business.Services;
public class UserService
{
    private readonly CartService _cartService;
    private readonly BillingService _billingService;

    public UserService(CartService cartService, BillingService billingService)
    {
        _billingService = billingService;
        _cartService = cartService;
    }

    public TraceableT<UserCheckoutResult> CheckoutUserCart(Guid userId)
    {
        return from cart in _cartService.GetCartForUser(userId)
               from charge in _billingService.ChargeCustomer(cart.Id, userId)
               select new UserCheckoutResult
               {
                   UserId = userId,
                   CartId = cart.Id,
                   Charged = charge
               };
    }

    public TraceableT<UserCheckoutResult> CheckoutExistingCart(CartService.Cart cart, Guid userId)
    {
        return from charge in _billingService.ChargeCustomer(cart.Id, userId)
               select new UserCheckoutResult
               {
                   UserId = userId,
                   CartId = cart.Id,
                   Charged = charge
               };
    }
}

public record UserCheckoutResult
{
    public Guid UserId { get; init; }
    public Guid CartId { get; init; }
    public required Fin<ChargeResult> Charged { get; init; }
}
