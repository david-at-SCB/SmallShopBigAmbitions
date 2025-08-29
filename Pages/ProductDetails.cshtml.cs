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

namespace SmallShopBigAmbitions.Pages;

public class ProductDetailsModel : PageModel
{
    private readonly ProductService _products;

    public ProductDetailsModel(ProductService products)
    {
        _products = products;
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

    public IActionResult OnPostAddToCart(int id)
    {
        Message = $"Added product {id} to cart.";
        return RedirectToPage(new { id });
    }
}