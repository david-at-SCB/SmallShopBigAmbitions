using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Cart.AddItemToCart;

public record AddItemToCartCommand(
    Guid UserId,
    ExternalProductRef Product,
    Quantity Quantity,
    Money PriceRef,
    string Source)
    : IFunctionalRequest<AddItemToCartDTO>;
