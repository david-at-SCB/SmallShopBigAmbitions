using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Carts.AddItemToCart;

public record AddItemToCartResult(
    Guid UserId,
    int APIProductId,
    int Quantity,
    DateTime AddedAt,
    Money PriceSnapshot,
    string Source,
    CartSnapshot Cart // resulting cart snapshot after persistence
);
