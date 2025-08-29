using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.Models.Mappers.ProductAPIToProductBusiness;
using LanguageExt;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application.Cart.AddItemToCart;

public class AddItemToCartHandler(
    ILogger<BillingService> logger,
    CartService cartService,
    UserService userService,
    ProductService productService) : IFunctionalHandler<AddItemToCartCommand, AddItemToCartDTO>
{
    private readonly ILogger<BillingService> _logger = logger;
    private readonly CartService _CartService = cartService;
    private readonly UserService _UserService = userService;
    private readonly ProductService _ProductService = productService;

    public IO<Fin<AddItemToCartDTO>> Handle(AddItemToCartCommand request, TrustedContext context, CancellationToken ct)
    {
        // Build a traceable pipeline: fetch cart -> add item(s) -> project to DTO
        var trace =
            from cart in _CartService.GetCartForUser(request.UserId)
            from productFin in _ProductService.GetProductById(request.APIProductId, ct)
            from updated in productFin.Match(
                Succ: dto =>
                {
                    var product = Mapper.MapToBusinessProduct(dto);
                    var items = Map<FakeStoreProduct, int>().Add(product, request.Quantity);
                    return _CartService.AddItems(cart, items);
                },
                Fail: _ =>
                    // If product fetch failed, just return the original cart unchanged
                    TraceableTLifts.FromIO(IO.lift(() => cart), spanName: "cart.add_items.skip")
            )
            select new AddItemToCartDTO(
                UserId: request.UserId,
                APIProductId: request.APIProductId,
                Quantity: request.Quantity,
                AddedAt: request.AddedAt,
                PriceSnapshot: request.PriceSnapshot,
                Currency: request.Currency,
                Source: request.Source
            );

        // Execute with tracing + convert to Fin
        return trace.RunTraceableFin(ct);
    }
}