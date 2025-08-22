using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Pages;

public class ProductsModel : PageModel
{
    public ProductsModel(IDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }   

    public void OnGet()
    {
        // This method is called when the page is accessed via a GET request.
        // You can add logic here to retrieve product data or perform other actions.
    }

    public List<Product> OnPostGoToProducts()
    {
        // This method is called when the form on the page is submitted.
        // You can add logic here to handle the form submission, such as redirecting to a products page.
        // For example, you might redirect to a different page that lists products.
        var context = GetTrustedContextSomeHow();
        var request = new GetAllProductsQuery();
        var products = _dispatcher.Dispatch(request, CancellationToken.None).RunAsync().Result;
        
        return Response.AppendTrailer(products);
    }
}
