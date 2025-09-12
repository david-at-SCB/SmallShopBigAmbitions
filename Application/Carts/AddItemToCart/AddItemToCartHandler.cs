using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.Database.Commands; 
using SmallShopBigAmbitions.Application._Abstractions;

using  SmallShopBigAmbitions.Monads.TraceableTransformer.Extensions.FinLinq;

namespace SmallShopBigAmbitions.Application.Carts.AddItemToCart;

public class AddItemToCartHandler(
    CartService cartService,
    ProductService productService,
    ICartPersistence cartPersistence)
    : IFunctionalHandler<AddItemToCartCommand, AddItemToCartResult>
{
    private readonly CartService _cartService = cartService;
    private readonly ProductService _productService = productService;
    private readonly ICartPersistence _persistence = cartPersistence;

    public IO<Fin<AddItemToCartResult>> Handle_pyramid(AddItemToCartCommand request, TrustedContext context, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Anonymous users: do not persist; return special error code (UI can show prompt)
        if (!context.IsAuthenticated)
            return IO.lift<Fin<AddItemToCartResult>>(() => Fin<AddItemToCartResult>.Fail(Error.New("cart.add.anonymous_not_persisted")));

        // FLOW (TraceableT<Fin<AddItemToCartResult>>):
        // Cart -> Policy -> Product -> Persist -> Result
        var flow =
            _cartService
                .GetCartForUser(request.UserId) // TraceableT<Fin<Cart>>
                .BindFin(cart =>
                    AddItemToCartPolicy
                        .Evaluate(request, context, cart.Items.Count) // TraceableT<Fin<Unit>>
                        .BindFin(_ =>
                            _productService // TraceableT<Fin<ProductDto>>
                                .GetProductById(request.Product.ApiProductId, ct)
                                .BindFin(productDto =>
                                {
                                    // Price snapshot: honor requested currency but use CURRENT amount from product service.
                                    var priceSnapshot = new Money(request.PriceRef.Currency, productDto.Price);
                                    var internalPid = ProductIdMapper.ToInternal(productDto.Id);

                                    var persist = TraceableTLifts.FromIOFin(
                                        _persistence.AddLine(
                                            cart.Id,
                                            request.UserId,
                                            internalPid.Value,
                                            request.Quantity.Value,
                                            priceSnapshot),
                                        spanName: "cart.persist.add_line");

                                    // Map CartSnapshot -> Result (we ignore snapshot content here but could enrich later)
                                    return persist.Map(fin => fin.Map(cartSnap => new AddItemToCartResult(
                                        UserId: request.UserId,
                                        APIProductId: request.Product.ApiProductId,
                                        Quantity: request.Quantity.Value,
                                        AddedAt: now,
                                        PriceSnapshot: priceSnapshot,
                                        Source: request.Source,
                                        Cart: cartSnap)));
                                })
                        )
                )
                .WithSpanName("cart.add_item.flow");

        return flow.RunTraceable(ct);
    }

    public IO<Fin<AddItemToCartResult>> Handle(AddItemToCartCommand request, TrustedContext context, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Anonymous users: do not persist; return special error code (UI can show prompt)
        if (!context.IsAuthenticated)
            return IO.lift<Fin<AddItemToCartResult>>(() => Fin<AddItemToCartResult>.Fail(Error.New("cart.add.anonymous_not_persisted")));

        // FLOW (TraceableT<Fin<AddItemToCartResult>>):
        // Cart -> Policy -> Product -> Persist -> Result
        var flow =
            from cart in (TraceableT<Fin<Models.Cart>>)_cartService.GetCartForUser(request.UserId)
            from _ in AddItemToCartPolicy.Evaluate(request, context, cart.Items.Count)
            from productDto in _productService.GetProductById(request.Product.ApiProductId, ct)
            let priceSnapshot = new Money(request.PriceRef.Currency, productDto.Price)
            let internalPid = ProductIdMapper.ToInternal(productDto.Id)
            from cartSnap in TraceableTLifts.FromIOFin(
                _persistence.AddLine(cart.Id, request.UserId, internalPid.Value, request.Quantity.Value, priceSnapshot),
            spanName: "cart.persist.add_line")

            select new AddItemToCartResult(
                UserId: request.UserId,
                APIProductId: request.Product.ApiProductId,
                Quantity: request.Quantity.Value,
                AddedAt: now,
                PriceSnapshot: priceSnapshot,
                Source: request.Source,
                Cart: cartSnap
            );

        return flow
            .WithSpanName("cart.add_item.flow")
            .RunTraceable(ct);
    }
}