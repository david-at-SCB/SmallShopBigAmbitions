using SmallShopBigAmbitions.Models;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Database;


public interface IDataAccess
{
    Cart GetUserCart(Guid User);
}

public class DataAccess : IDataAccess
{
    public Cart GetUserCart(Guid UserId)
    {
        if (UserId == Guid.Empty)
        {
            throw new Exception("User not found");
        }
        else
            return new Cart
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                Items = Map<FakeStoreProduct, int>()
            };
    }
}