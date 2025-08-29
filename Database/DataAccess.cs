using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Database;


public interface IDataAccess
{
    public TraceableT<Customer> GetCustomerById(Guid userId);
    public Cart GetCustomerCart(Guid User);
}

public class DataAccess : IDataAccess
{
    public TraceableT<Customer> GetCustomerById(Guid userId)
    {
        throw new NotImplementedException();
    }

    public Cart GetCustomerCart(Guid UserId)
    {
        if (UserId == Guid.Empty)
        {
            throw new Exception("Cannot create a Cart for non-existent customer");
        }
        else
        {
            // Create an empty cart for the given user
            return Cart.Empty(UserId);
        }
    }
}