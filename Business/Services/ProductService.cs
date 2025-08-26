using SmallShopBigAmbitions.HTTP;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using System.Text;
using FunctionalHttpClient = SmallShopBigAmbitions.HTTP.FunctionalHttpClient;

namespace SmallShopBigAmbitions.Business.Services;

public class ProductService(FunctionalHttpClient http, ILogger<ProductService> logger)
{
    private readonly FunctionalHttpClient _http = http;
    private readonly ILogger<ProductService> _logger = logger;

    public record ProductDto(
        int Id,
        string Title,
        string Description,
        decimal Price,
        string Category,
        string Image,
        double RatingRate,
        int RatingCount
    );

    public TraceableT<Fin<ProductDto>> GetProductById(int id, CancellationToken ct, int maxRetries = 3) =>
     TraceableTExtensions.WithTracingAndRetry(
         spanName: $"DataBase.GetProductById({id})",
         effect: _http.GetJsonFinAsync<ProductDto>($"https://fakestoreapi.com/products/{id}", ct),
         maxRetries: maxRetries
     )
     .WithAttributes(fin => HttpTelemetryAttributes.FromFin(fin, $"https://fakestoreapi.com/products/{id}"))
     .WithLogging(_logger);

    public TraceableT<Fin<Seq<ProductDto>>> GetAllProducts(
        int maxRetries = 3) =>
        TraceableTExtensions.WithTracingAndRetry(
            spanName: "ProductService.GetAllProducts",
            effect: IO.liftAsync<Fin<Seq<ProductDto>>>(async () => // this IO effect is async?
            {
                var products = _http.GetJsonAsync<ProductDto[]>("https://fakestoreapi.com/products");

                return products
                    .Match<Fin<Seq<ProductDto>>>(
                        ps => FinSucc(Seq(ps.AsEnumerable())),
                        () => FinFail<Seq<ProductDto>>(Error.New("No products found"))
                    );
            }),
            maxRetries: maxRetries
        )
        .WithAttributes(fin => fin.Match<IEnumerable<KeyValuePair<string, object>>>(
            Succ: seq =>
            {
                var count = seq.Fold(0, (acc, _) => acc + 1);
                var titles = seq.Fold(new StringBuilder(), (sb, p) =>
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(p.Title);
                    return sb;
                }).ToString();

                return
                [
                    new KeyValuePair<string, object>("ProductCount", count),
                    new KeyValuePair<string, object>("Description", titles)
                ];
            },
            Fail: err =>
            [
                new KeyValuePair<string, object>("ProductCount", 0),
                new KeyValuePair<string, object>("Error", err.Message)
            ]
        ))
        .WithLogging(_logger);

    internal async Task<List<FakeStoreProduct>> GetProductsAsync(CancellationToken ct)
    {
        var bunchaProducts = GetAllProducts(maxRetries: 5) // await?
            .RunTraceable(ct)
            .Run();
        return bunchaProducts.Match(
            Succ: products => [.. products.Map(p => new FakeStoreProduct(
                Id: p.Id,
                Title: p.Title,
                Price: p.Price,
                Description: p.Description,
                Category: p.Category,
                Image: p.Image,
                Rating: new FakeStoreRating(
                    Rate: p.RatingRate,
                    Count: p.RatingCount
                    )
            ))],
            Fail: err =>
            {
                _logger.LogError("Failed to fetch products: {Error}", err.Message);
                return new List<FakeStoreProduct>();
            }
        );
    }
}