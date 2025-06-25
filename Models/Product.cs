namespace SmallShopBigAmbitions.Models;

public record Product
{
    private Guid Id { get; init; } = Guid.NewGuid();
    public string? Name { get; init; }
    public string? Description { get; init; }
    public decimal Price { get; init; }
}