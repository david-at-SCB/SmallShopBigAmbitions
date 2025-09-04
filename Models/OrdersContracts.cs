namespace SmallShopBigAmbitions.Models;

public enum OrderStatus
{
    Created,
    Authorized,
    Captured,
    Canceled,
    Refunded
}

public sealed record OrderSnapshot(
    Guid OrderId,
    Guid UserId,
    CartSnapshot CartSnapshot,
    Money Subtotal,   // items-only subtotal (copied from cart)
    Money Discount,   // positive discount amount
    Money Shipping,
    Money Tax,
    Money Total,
    OrderStatus Status,
    DateTimeOffset CreatedAt
);
