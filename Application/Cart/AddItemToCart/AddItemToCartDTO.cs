using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Cart.AddItemToCart;

public record AddItemToCartDTO(Guid UserId, Guid ProductId, int Quantity, DateTime AddedAt, decimal PriceSnapshot, Currency Currency, string Source);