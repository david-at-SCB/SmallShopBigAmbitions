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

namespace SmallShopBigAmbitions.Pages;

public class ProductsModel : PageModel
{
    private readonly ProductService _productService;

    public ProductsModel(ProductService products)
    {
        _productService = products;
    }

    public List<FakeStoreProduct> Products { get; private set; } = [];

    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        Products = await _productService.GetProductsAsync(ct);
    }

    public IActionResult OnPostAddToCart(int id)
    {
        // TODO: integrate with real cart persistence. For now just show a message.
        Message = $"Added product {id} to cart.";
        // cart service. AddToCart(userId,productid); // This would be a call to your cart service
        // wait for user
        // update page without reload, only reload the cart if necessary.
        return RedirectToPage();
    }
}
