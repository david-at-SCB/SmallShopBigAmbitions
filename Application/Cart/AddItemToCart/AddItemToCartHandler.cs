using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Monads.TraceableTransformer;

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
            // For now, we store product reference as a string (can evolve to Map<Product, int>)
            from updated in _CartService.AddItems(cart, [request.ProductId.ToString()])
            select new AddItemToCartDTO(
                UserId: request.UserId, 
                ProductId: request.ProductId,
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