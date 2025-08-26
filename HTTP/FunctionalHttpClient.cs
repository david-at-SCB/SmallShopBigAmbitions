using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.TracingSources;
using System.Diagnostics;
using static SmallShopBigAmbitions.Business.Services.ProductService;

namespace SmallShopBigAmbitions.HTTP;

public interface IHttpClient
{
    /// <summary>
    /// Performs a GET request and returns the raw HTTP response as an IO effect.
    /// </summary>
    IO<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a GET request and deserializes the response into a DTO.
    /// </summary>
    IO<T?> GetJsonAsync<T>(string requestUri, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a GET request and returns a Fin-wrapped DTO for error-safe composition.
    /// </summary>
    IO<Fin<T>> GetJsonFinAsync<T>(string requestUri, CancellationToken cancellationToken = default);
}

public class FunctionalHttpClient(System.Net.Http.HttpClient httpClient) : IHttpClient, IDisposable
{
    private readonly System.Net.Http.HttpClient _httpClient = httpClient;
    private readonly ILogger<FunctionalHttpClient> _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<FunctionalHttpClient>();
    private readonly ActivitySource _activitySource = ShopActivitySource.Instance;


    public void Dispose() => _httpClient.Dispose();

    public IO<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken = default) =>
            IO.liftAsync(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await _httpClient.GetAsync(requestUri, cancellationToken);
            });

    public IO<T?> GetJsonAsync<T>(string requestUri, CancellationToken cancellationToken = default) =>
        IO.liftAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var activity = ShopActivitySource.Instance.StartActivity("HttpClient.GetJsonAsync", ActivityKind.Client);
            activity?.SetTag("http.method", "GET");
            activity?.SetTag("http.url", requestUri);

            try
            {
                var response = await _httpClient.GetAsync(requestUri, cancellationToken);
                activity?.SetTag("http.status_code", (int)response.StatusCode);

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
                activity?.SetTag("http.response_size", response.Content.Headers.ContentLength ?? 0);
                activity?.SetTag("http.success", true);

                return result;
            }
            catch (Exception ex)
            {
                activity?.SetTag("http.success", false);
                activity?.SetTag("error", true);
                activity?.SetTag("exception.type", ex.GetType().Name);
                activity?.SetTag("exception.message", ex.Message);
                activity?.SetTag("exception.stacktrace", ex.StackTrace);
                throw;
            }
        });

    public IO<Fin<T>> GetJsonFinAsync<T>(string requestUri, CancellationToken cancellationToken = default) =>
        IO.liftAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stopwatch = Stopwatch.StartNew();
            using var activity = _activitySource.StartActivity("HttpClient.GetJsonFinAsync", ActivityKind.Client);
            activity?.SetTag("http.method", "GET");
            activity?.SetTag("http.url", requestUri);

            try
            {
                var response = await _httpClient.GetAsync(requestUri, cancellationToken);
                activity?.SetTag("http.status_code", (int)response.StatusCode);

                if (!response.IsSuccessStatusCode)
                    return FinFail<T>(Error.New($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"));

                var json = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
                stopwatch.Stop();

                activity?.SetTag("http.success", json is not null);
                activity?.SetTag("http.response_size", response.Content.Headers.ContentLength ?? 0);
                activity?.SetTag("http.duration_ms", stopwatch.ElapsedMilliseconds);

                return json is not null
                    ? FinSucc(json)
                    : FinFail<T>(Error.New("Null response"));
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                activity?.SetTag("http.success", false);
                activity?.SetTag("error", true);
                activity?.SetTag("exception.type", ex.GetType().Name);
                activity?.SetTag("exception.message", ex.Message);
                activity?.SetTag("exception.stacktrace", ex.StackTrace);
                activity?.SetTag("http.duration_ms", stopwatch.ElapsedMilliseconds);

                return FinFail<T>(Error.New(ex));
            }
        });

    public TraceableT<Fin<Seq<ProductDto>>> GetAllProductsPaged(
        int pageSize = 20,
        int maxRetries = 3,
        CancellationToken ct = default)
    {
        // Recursive helper to fetch and accumulate pages
        TraceableT<Fin<Seq<ProductDto>>> Loop(int page, Seq<ProductDto> acc) =>
            PaginateAllProducts(page, pageSize, maxRetries, ct)
                .Bind(fin => fin.Match(
                    Succ: products =>
                        products.IsEmpty
                            ? new TraceableT<Fin<Seq<ProductDto>>>(
                                IO.liftAsync(async () => FinSucc(acc)),
                                "GetAllProductsPaged.Complete")
                            : Loop(page + 1, acc + products),
                    Fail: err => new TraceableT<Fin<Seq<ProductDto>>>(
                        IO.liftAsync(async () => FinFail<Seq<ProductDto>>(err)),
                        "GetAllProductsPaged.Fail")
                ));

        return Loop(1, Empty);
    }

    public TraceableT<Fin<Seq<ProductDto>>> PaginateAllProducts(
    int page,
    int pageSize,
    int maxRetries = 3,
    CancellationToken ct = default)
    {
        var offset = (page - 1) * pageSize;
        var limit = pageSize;
        var uri = $"https://api.escuelajs.co/api/v1/products?offset={offset}&limit={limit}";

        return TraceableTExtensions.WithTracingAndRetry(
            spanName: $"ProductService.PaginateAllProducts(page={page}, size={pageSize})",
            effect: GetJsonFinAsync<ProductDto[]>(uri, ct)
                .Map(fin => fin.Map(products => Seq(products.AsEnumerable()))),
            maxRetries: maxRetries
        )
        .WithAttributes(fin => HttpTelemetryAttributes.FromFin(fin, uri))
        .WithLogging(_logger);
    }
}