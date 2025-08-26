using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Application.Products.GetAllProducts;

public record GetAllProductsQuery(string url) :IFunctionalRequest<GetAllProductsResponse>
{
}

public record GetAllProductsResponse
{
    public required IEnumerable<FakeStoreProduct> Products { get; init; }
    public DateTime FetchedAt { get; init; } 
    public required string Url { get; init; }
}