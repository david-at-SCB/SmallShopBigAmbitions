using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Cart.AddItemToCart;

public record AddItemToCartCommand(Guid UserId, Guid ProductId, int Quantity, DateTime AddedAt, decimal PriceSnapshot, Currency Currency, string Source)
    : IFunctionalRequest<AddItemToCartDTO>
{

}
