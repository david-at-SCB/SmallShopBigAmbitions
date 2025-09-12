using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Cart.RemoveItemFromCart;

public record RemoveItemFromCartCommand(Guid UserId, ProductId ProductId, string Source)
    : IFunctionalRequest<RemoveItemFromCartDTO>;
