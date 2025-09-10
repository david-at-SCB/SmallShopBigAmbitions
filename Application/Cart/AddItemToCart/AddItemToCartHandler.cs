using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application.Cart.AddItemToCart;

public class AddItemToCartHandler(
    ILogger<BillingService> logger,
    CartService cartService,
    ProductService productService) : IFunctionalHandler<AddItemToCartCommand, AddItemToCartDTO>
{
    private readonly ILogger<BillingService> _logger = logger;
    private readonly CartService _cartService = cartService;
    private readonly ProductService _productService = productService;

    public IO<Fin<AddItemToCartDTO>> Handle(AddItemToCartCommand request, TrustedContext context, CancellationToken ct)
    {
        var now = DateTime.UtcNow; // authoritative server timestamp

        var flow = _cartService
            .GetCartForUser(request.UserId)
            .Bind(cartFin => cartFin.Match(
                Succ: cart => AddItemToCartPolicy
                    .Evaluate(request, context, cart.Items.Count)
                    .Bind(policyFin => policyFin.Match(
                        Succ: _ => _productService
                            .GetProductById(request.Product.ApiProductId, ct)
                            .Bind(prodFin => prodFin.Match(
                                Succ: productDto =>
                                {
                                    var internalPid = new ProductId(Guid.NewGuid()); // TODO stable mapping
                                    var priceSnapshot = new Money(request.PriceRef.Currency, productDto.Price);
                                    var additions = LanguageExt.HashMap<ProductId, CartLine>.Empty.Add(
                                        internalPid,
                                        new CartLine(internalPid, request.Quantity.Value, priceSnapshot));

                                    return _cartService
                                        .AddItems(cart, additions)
                                        .Map(updatedFin => updatedFin.Map(_ => new AddItemToCartDTO(
                                            UserId: request.UserId,
                                            APIProductId: request.Product.ApiProductId,
                                            Quantity: request.Quantity.Value,
                                            AddedAt: now,
                                            PriceSnapshot: priceSnapshot,
                                            Source: request.Source)));
                                },
                                Fail: e => TraceableTLifts.FromFin(Fin<AddItemToCartDTO>.Fail(e), "product.fetch.fail", _ => []))
                            ),
                        Fail: e => TraceableTLifts.FromFin(Fin<AddItemToCartDTO>.Fail(e), "policy.fail", _ => []))) ,
                Fail: e => TraceableTLifts.FromFin(Fin<AddItemToCartDTO>.Fail(e), "cart.fetch.fail", _ => [])))
            .WithSpanName("cart.add_item.flow")
            .WithLogging(_logger);

        return flow.RunTraceable(ct);
    }
}