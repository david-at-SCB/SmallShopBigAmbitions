using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Models.Mappers.ProductAPIToProductBusiness;

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

    public OrderModel(IFunctionalDispatcher mediator, ProductService ProductService)
    {
        _dispatcher = mediator;
        _ProductService = ProductService;
    }

    public Fin<Cart> Cart { get; private set; }

    // Expose selected product details (from ProductDetails page) on Order page if needed
    public Option<FakeStoreProduct> SelectedProduct { get; private set; }

    public async Task OnGetAsync(Guid userId, int productId, CancellationToken ct)
    {
        var trustedContext = TrustedContextFactory.FromHttpContext(HttpContext);

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