namespace SmallShopBigAmbitions.Models;

public record Cart(Guid Id, Guid CustomerId, Map<FakeStoreProduct, int> Items)
{
    public static Cart Empty(Guid userId) => new(Guid.NewGuid(), userId, Map<FakeStoreProduct, int>());
}