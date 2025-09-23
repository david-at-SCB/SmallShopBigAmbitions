// Async/IO checklist (Razor Pages + LanguageExt)
// - Handlers return Task/Task<IActionResult>; always await RunTraceable(ct).RunAsync().
// - Avoid .Run(), .Result, .Wait() on IO/Aff in web code.
// - Accept and pass CancellationToken to all effects/HTTP calls.
// - UI layer calls service async wrappers (Task<T>) and maps Fin/Option to IActionResult.
// - Use Fin/Option for errors; handle via Match and set Model state/data.
// - Keep heavy work in services; pages should be thin adapters.
// - Pass CancellationToken from handler to service.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Models.Mappers.ProductAPIToProductBusiness;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Application.Carts.AddItemToCart;
using SmallShopBigAmbitions.Application._Abstractions;

namespace SmallShopBigAmbitions.Pages;

public class ProductDetailsModel : PageModel
{
    private readonly ProductService _products;
    private readonly IFunctionalDispatcher _dispatcher;

    public ProductDetailsModel(ProductService products, IFunctionalDispatcher dispatcher)
    {
        _products = products;
        _dispatcher = dispatcher;
    }

    public FakeStoreProduct? Product { get; private set; }

    [TempData]
    public string? Message { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        var fin = await _products.GetProductById(id, ct)
            .RunTraceable(ct)
            .RunAsync();

        return fin.Match<IActionResult>(
            Succ: dto =>
            {
                Product = Mapper.MapToBusinessProduct(dto);
                return Page();
            },
            Fail: _ => NotFound()
        );
    }

    public async Task<IActionResult> OnPostAddToCartAsync(int id, int quantity = 1, CancellationToken ct = default)
    {
        if (quantity <= 0) quantity = 1;

        var productFin = await _products.GetProductById(id, ct)
            .RunTraceable(ct)
            .RunAsync();

        Fin<AddItemToCartResult> final;
        if (productFin.IsFail)
        {
            var err = productFin.Match(_ => Error.New("unreachable"), e => e);
            final = Fin<AddItemToCartResult>.Fail(err);
        }
        else
        {
            var dto = productFin.Match(p => p, _ => default!);
            var qtyFin = Quantity.Create(quantity);
            if (qtyFin.IsFail)
            {
                var err = qtyFin.Match(_ => Error.New("unreachable"), e => e);
                final = Fin<AddItemToCartResult>.Fail(err);
            }
            else
            {
                var qtyObj = qtyFin.Match(q => q, _ => default);
                var currency = "SEK";
                var cmd = new AddItemToCartCommand(
                    UserId: EnsureUserId(),
                    Product: new ExternalProductRef(dto.Id, CatalogProvider.FakeStore),
                    Quantity: qtyObj,
                    PriceRef: new Money(currency, dto.Price),
                    Source: "ui.product.details");
                final = await _dispatcher.Dispatch<AddItemToCartCommand, AddItemToCartResult>(cmd, ct).RunAsync();
            }
        }

        var _ = final.Match(
            Succ: r => Message = $"Added product {r.APIProductId} (x{r.Quantity}). Cart lines: {r.Cart.Items.Count}",
            Fail: e => Message = e.Message == "cart.add.anonymous_not_persisted"
                ? "Login or impersonate a user to persist cart items."
                : $"Could not add to cart: {e.Message}"
        );

        return RedirectToPage(new { id });
    }

    private Guid EnsureUserId()
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(idClaim, out var gid)) return gid;
        }

        const string CookieName = "anon-id";
        if (Request.Cookies.TryGetValue(CookieName, out var raw) && Guid.TryParse(raw, out var g))
            return g;
        var id = Guid.NewGuid();
        Response.Cookies.Append(CookieName, id.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddYears(1)
        });
        return id;
    }
}