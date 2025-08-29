using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Cart.AddItemToCart;

public record AddItemToCartCommand(Guid UserId, int APIProductId, int Quantity, DateTime AddedAt, decimal PriceSnapshot, Currency Currency, string Source)
    : IFunctionalRequest<AddItemToCartDTO>
{

}
