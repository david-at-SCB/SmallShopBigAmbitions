namespace SmallShopBigAmbitions.Business.Services;

using SmallShopBigAmbitions.Monads.TraceableTransformer;

public class UserService
{

    private readonly CartService _cartService;
    private readonly BillingService _billingService;

    public UserService(CartService cartService, BillingService billingService)
    {
        _billingService = billingService;
        _cartService = cartService;
    }

    /// <summary>
    /// MediatR makes sure only a authorized service can call this method.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static TraceableT<UserCheckoutResult> CheckoutUserCart(Guid userId, ILogger logger)
    {
        return from cart in CartService.GetCartForUser(userId, logger)
               from charge in BillingService.ChargeCustomer(cart.Id, userId, logger)
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
    public required Fin<ChargeResult> Charged { get; init; } // Fin<T> requires keyword "required" for nullability annotations.
}
