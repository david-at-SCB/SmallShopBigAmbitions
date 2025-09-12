using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Carts.AddItemToCart;

public record AddItemToCartCommand(
    Guid UserId,
    ExternalProductRef Product,
    Quantity Quantity,
    Money PriceRef,
    string Source)
    : IFunctionalRequest<AddItemToCartResult>;
