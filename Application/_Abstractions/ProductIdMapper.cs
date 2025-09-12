using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application._Abstractions;

/// <summary>
/// Deterministic mapping from external (API) product integer id to internal Guid-based ProductId.
/// Ensures stable identity across sessions without database indirection.
/// </summary>
public static class ProductIdMapper
{
    public static ProductId ToInternal(int apiProductId)
    {
        // 16-byte array zeroed except first 4 bytes hold the int; remaining ensure deterministic Guid.
        var bytes = new byte[16];
        BitConverter.GetBytes(apiProductId).CopyTo(bytes, 0);
        return new ProductId(new Guid(bytes));
    }
}
