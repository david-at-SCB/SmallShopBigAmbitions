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
        var apiProduct = _products.GetProductById(id, ct);
        Product = Mapper.MapToBusinessProduct(apiProduct.RunTraceable(ct).Run().Match(
            (hejs) => hejs.));
        if (Product is null)
            return NotFound();
        return Page();
    }

    public IActionResult OnPostAddToCart(int id)
    {
        Message = $"Added product {id} to cart.";
        return RedirectToPage(new { id });
    }
}