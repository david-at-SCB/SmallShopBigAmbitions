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
