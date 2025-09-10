using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Cart.RemoveItemFromCart;

public sealed record RemoveItemFromCartDTO(Guid UserId, ProductId ProductId, int RemovedQuantity, DateTime RemovedAt, string Source);
    