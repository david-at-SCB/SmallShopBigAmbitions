using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Models.Mappers.ProductAPIToProductBusiness;
using SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay; // IntentToPayDto
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;

namespace SmallShopBigAmbitions.Pages;

// Async/IO checklist (Razor Pages + LanguageExt)
// - Handlers return Task/Task<IActionResult>; always await RunTraceable(ct).RunAsync().
// - Avoid .Run(), .Result, .Wait() on IO/Aff in web code.
// - Accept and pass CancellationToken to all effects/HTTP calls.
// - UI layer calls service async wrappers (Task<T>) and maps Fin/Option to IActionResult.
// - Use Fin/Option for errors; handle via Match and set Model state/data.
// - Keep heavy work in services; pages should be thin adapters.
// - Pass CancellationToken from handler to service.
public class OrderModel : PageModel
{
    private readonly IFunctionalDispatcher _dispatcher;
    private readonly ProductService _ProductService;
    private readonly IHttpClientFactory _httpClientFactory;

    public OrderModel(IFunctionalDispatcher mediator, ProductService ProductService, IHttpClientFactory httpClientFactory)
    {
        _dispatcher = mediator;
        _ProductService = ProductService;
        _httpClientFactory = httpClientFactory;
    }

    public Fin<Cart> Cart { get; private set; }

    // Expose selected product details (from ProductDetails page) on Order page if needed
    public Option<FakeStoreProduct> SelectedProduct { get; private set; }

    [TempData]
    public string? PaymentIntentKey { get; set; }

    public async Task OnGetAsync(Guid userId, int productId, CancellationToken ct)
    {
        var trustedContext = TrustedContextFactory.FromHttpContext(HttpContext);

        Cart = await _dispatcher.Dispatch<GetCartForUserQuery, Cart>(new GetCartForUserQuery(userId), ct).RunAsync();

        var prodFin = _ProductService.GetProductById(productId, ct, maxRetries: 5).RunTraceable(ct).Run();

        // this should be a Mapper from ProductDto -> FakeStoreProduct
        // Map Fin<ProductDto> -> Option<FakeStoreProduct>
        SelectedProduct = prodFin.Match(
            Succ: dto => Mapper.MapToBusinessProduct(dto),
            Fail: _ => Option<FakeStoreProduct>.None
        );
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostCreatePaymentIntentAsync(Guid userId, Guid cartId, string currency = "SEK", string? shippingAddress = null, CancellationToken ct = default)
    {
        // Generate once and reuse across retries; persist in TempData
        if (string.IsNullOrWhiteSpace(PaymentIntentKey))
        {
            PaymentIntentKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        }

        var query = $"/api/Billing/payment_intent?UserId={userId}&CartId={cartId}&Currency={currency}" +
                    (string.IsNullOrWhiteSpace(shippingAddress) ? string.Empty : $"&ShippingAddress={Uri.EscapeDataString(shippingAddress)}");

        var client = _httpClientFactory.CreateClient();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        using var req = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(baseUrl), query));
        req.Headers.Add("Idempotency-Key", PaymentIntentKey);

        using var resp = await client.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            // Keep key for retry and surface error on page
            ModelState.AddModelError(string.Empty, $"Failed to create payment intent: {(int)resp.StatusCode} {resp.ReasonPhrase}");
            return Page();
        }

        // Optional: parse intent DTO if the UI needs it
        var dto = await resp.Content.ReadFromJsonAsync<IntentToPayDto>(cancellationToken: ct);
        if (dto is null)
        {
            ModelState.AddModelError(string.Empty, "Payment intent created but response was empty.");
            return Page();
        }

        // If you need the client secret on the page, store it in TempData or ViewData here.
        ViewData["PaymentProvider"] = dto.Provider;
        ViewData["PaymentClientSecret"] = dto.ClientSecret;
        ViewData["PaymentIntentId"] = dto.PaymentIntentId;

        return Page();
    }
}