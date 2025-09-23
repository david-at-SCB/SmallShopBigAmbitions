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
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Application.Carts.AddItemToCart;
using SmallShopBigAmbitions.Application._Abstractions;

namespace SmallShopBigAmbitions.Pages;

public class ProductsModel : PageModel
{
    private readonly ProductService _productService;
    private readonly IFunctionalDispatcher _dispatcher;
    private readonly UserService _userService;
    public ProductsModel(ProductService products, IFunctionalDispatcher dispatcher, UserService userService)
    {
        _productService = products;
        _dispatcher = dispatcher;
        _userService = userService;
    }

    public List<FakeStoreProduct> Products { get; private set; } = [];

    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Products = await _productService.GetProductsAsync(ct);
    }

    public async Task<IActionResult> OnPostAddToCartAsync(int id, decimal price, string currency, int quantity = 1, CancellationToken ct = default)
    {
        var normalizedQty = quantity <= 0 ? 1 : quantity;
        var qtyFin = Quantity.Create(normalizedQty);

        var resultFin = await qtyFin.Match(
            Succ: async q =>
            {
                var (userId, _, _) = _userService.EnsureUserId(HttpContext);
                var cmd = new AddItemToCartCommand(
                    UserId: userId,
                    Product: new ExternalProductRef(id, CatalogProvider.FakeStore),
                    Quantity: q,
                    PriceRef: new Money(currency, price),
                    Source: "ui.products.list");
                return await _dispatcher.Dispatch<AddItemToCartCommand, AddItemToCartResult>(cmd, ct).RunAsync();
            },
            Fail: e => Task.FromResult(Fin<AddItemToCartResult>.Fail(e))
        );

        Message = resultFin.Match(
            Succ: r => $"Added product {r.APIProductId} (x{r.Quantity}). Cart now has {r.Cart.Items.Count} line(s).",
            Fail: e => e.Message == "cart.add.anonymous_not_persisted" ?
                "Login or impersonate to persist cart items." :
                $"Add failed: {e.Message}");

        return RedirectToPage();
    }
}
