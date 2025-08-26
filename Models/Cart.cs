namespace SmallShopBigAmbitions.Models;

public record Cart
{
    public Guid Id { get; init; }
    public Map<FakeStoreProduct, int> Items { get; init; } = Map<FakeStoreProduct, int>();
    public Guid UserId { get; init; }
}