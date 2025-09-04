namespace SmallShopBigAmbitions.Models;

public readonly record struct ProductId(Guid Value);

/// Canonical cart line in the business layer
public sealed record CartLine(
    ProductId ProductId,
    int Quantity,
    Money UnitPrice);

/// Canonical cart snapshot in the business layer
public sealed record CartSnapshot(
    Guid CartId,
    Guid UserId,
    Map<ProductId, CartLine> Items,     // qty + snapped unit price
    Money Subtotal,  // denormalized snapshot (can be recomputed)
    string Country,
    string Region);
