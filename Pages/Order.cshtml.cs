using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Models.Mappers.ProductAPIToProductBusiness;

namespace SmallShopBigAmbitions.Pages;

public class OrderModel : PageModel
{
    private readonly IFunctionalDispatcher _dispatcher;
    private readonly ProductService _ProductService;

    public OrderModel(IFunctionalDispatcher mediator, ProductService ProductService)
    {
        _dispatcher = mediator;
        _ProductService = ProductService;
    }

    public Fin<CustomerCart> Cart { get; private set; }

    // Expose selected product details (from ProductDetails page) on Order page if needed
    public Option<FakeStoreProduct> SelectedProduct { get; private set; }

    public async Task OnGetAsync(Guid userId, int productId, CancellationToken ct)
    {
        // this should be injected?
        var trustedContext = new TrustedContext
        {
            CallerId = Guid.NewGuid(),
            Role = "Service",
            Token = Request.Headers.Authorization.ToString()
        };

        Cart = await _dispatcher.Dispatch(new GetCartForUserQuery(userId), ct).RunAsync();

        var prodFin = _ProductService.GetProductById(productId, ct, maxRetries: 5).RunTraceable(ct).Run();

        // this should be a Mapper from ProductDto -> FakeStoreProduct
        // Map Fin<ProductDto> -> Option<FakeStoreProduct>
        SelectedProduct = prodFin.Match(
            Succ: dto => Mapper.MapToBusinessProduct(dto),
            Fail: _ => Option<FakeStoreProduct>.None
            );
    }
}