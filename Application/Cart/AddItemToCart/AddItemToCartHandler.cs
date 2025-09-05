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
    private readonly CartService _cartService = cartService;
    private readonly UserService _userService = userService;
    private readonly ProductService _productService = productService;

    public IO<Fin<AddItemToCartDTO>> Handle(AddItemToCartCommand request, TrustedContext context, CancellationToken ct)
    {
        var flow = _cartService
            .GetCartForUser(request.UserId)
            .Bind(cartFin => cartFin.Match(
                Succ: cart => _productService.GetProductById(request.APIProductId, ct)
                    .Bind(prodFin => prodFin.Match(
                        Succ: dto =>
                        {
                            var businessProduct = Mapper.MapToBusinessProduct(dto);
                            // Build additions as HashMap<ProductId,CartLine>
                            var pid = new ProductId(Guid.NewGuid()); // TODO: stable mapping external->internal
                            var line = new CartLine(pid, request.Quantity, request.PriceSnapshot);
                            var additions = LanguageExt.HashMap<ProductId, CartLine>.Empty.Add(pid, line);
                            return _cartService.AddItems(cart, additions)
                                .Map(updatedFin => updatedFin.Map(updated => new AddItemToCartDTO(
                                    UserId: request.UserId,
                                    APIProductId: request.APIProductId,
                                    Quantity: request.Quantity,
                                    AddedAt: request.AddedAt,
                                    PriceSnapshot: request.PriceSnapshot,                                    
                                    Source: request.Source)));
                        },
                        Fail: e => TraceableTLifts.FromFin(Fin<AddItemToCartDTO>.Fail(e), "product.fetch.fail", _ => [])
                    )),
                Fail: e => TraceableTLifts.FromFin(Fin<AddItemToCartDTO>.Fail(e), "cart.fetch.fail", _ => [])
            ))
            .WithSpanName("cart.add_item.flow")
            .WithLogging(_logger);

        return flow.RunTraceable(ct);
    }
}