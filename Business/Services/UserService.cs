using SmallShopBigAmbitions.Application.Billing.CheckoutUser;
using SmallShopBigAmbitions.Application.Cart.AddItemToCart;
using SmallShopBigAmbitions.Database;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;

namespace SmallShopBigAmbitions.Business.Services;
public class UserService
{
    private readonly CartService _cartService;
    private readonly BillingService _billingService;
    private readonly IDataAccess _dataAccess;

    public UserService(IDataAccess DataAccess, CartService cartService, BillingService billingService)
    {
        _billingService = billingService;
        _cartService = cartService;
        
    }

    public TraceableT<CheckoutUserResultDTO> CheckoutUserCart(Guid userId)
    {
        var result = from cart in _cartService.GetCartForUser(userId)
               from charge in _billingService.ChargeCustomer(cart.Id, userId)
               select new CheckoutUserResultDTO
               {
                   UserId = userId,
                   CartId = cart.Id,
                   Charged = charge
               };
        // TODO: persist the result of the checkout operation
        //var persistRecord = Mappers.Map(result);
        //_dataAccess.Save(persistRecord);
        return result;
    }

    public TraceableT<CheckoutUserResultDTO> CheckoutExistingCart(CustomerCart cart, Guid userId)
    {
        return from charge in _billingService.ChargeCustomer(cart.Id, userId)
               select new CheckoutUserResultDTO
               {
                   UserId = userId,
                   CartId = cart.Id,
                   Charged = charge
               };
    }

    internal User GetUserById(Guid userId)
    {
        throw new NotImplementedException();
    }
}
