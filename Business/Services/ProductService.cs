// Async/IO checklist (Razor Pages + LanguageExt)
// - Handlers return Task/Task<IActionResult>; always await RunTraceable(ct).RunAsync().
// - Avoid .Run(), .Result, .Wait() on IO/Aff in web code.
// - Accept and pass CancellationToken to all effects/HTTP calls.
// - Services: expose composable TraceableT/IO; add Task wrappers (e.g., GetXAsync) for UI.
// - Use Fin/Option for errors; handle via Match in handlers and return proper IActionResult.
// - Map DTO -> domain in a mapper; keep IO layer DTO-focused.
// - Ensure DI registers FunctionalHttpClient and service; no static singletons.
// - Add .WithLogging and telemetry attributes where useful.
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
     .WithAttributes(fin => HttpTelemetryAttributes.FromFin(fin, $"https://fakestoreapi.com/products/{id}"));


    public TraceableT<Fin<Seq<ProductDto>>> GetAllProducts(int maxRetries = 3, CancellationToken ct = default)
    {
        const string uri = "https://fakestoreapi.com/products";

        return TraceableTExtensions.WithTracingAndRetry(
            spanName: "ProductService.GetAllProducts",
            effect: _http.GetJsonFinAsync<ProductDto[]>(uri, ct)
                .Map(fin => fin.Map(products => Seq(products.AsEnumerable()))),
            maxRetries: maxRetries
        )
        .WithAttributes(fin => HttpTelemetryAttributes.FromFin(fin, uri));
    }

    internal async Task<List<FakeStoreProduct>> GetProductsAsync(CancellationToken ct)
    {
        var fin = await GetAllProducts(maxRetries: 5, ct).RunTraceable(ct).RunAsync();

        return fin.Match(
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