using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using static LanguageExt.Prelude;
using CartModel = SmallShopBigAmbitions.Models.Cart;

namespace SmallShopBigAmbitions.Application.Cart.RemoveItemFromCart;

public class RemoveItemFromCartHandler(
    CartService cartService) : IFunctionalHandler<RemoveItemFromCartCommand, RemoveItemFromCartDTO>
{
    private readonly CartService _cartService = cartService;

    public IO<Fin<RemoveItemFromCartDTO>> Handle(RemoveItemFromCartCommand request, TrustedContext context, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Local functions for clarity and reduced verbosity in the main flow
        TraceableT<Fin<RemoveItemFromCartDTO>> BuildResult(CartModel updatedCart, int removedQty) =>
            _cartService
                .AddItems(updatedCart, new LanguageExt.HashMap<ProductId, CartLine>())
                .Map(saved => saved.Map(_ => new RemoveItemFromCartDTO(
                    UserId: request.UserId,
                    ProductId: request.ProductId,
                    RemovedQuantity: removedQty,
                    RemovedAt: now,
                    Source: request.Source)));

        // Local function for clarity and reduced verbosity in the main flow
        TraceableT<Fin<(CartModel cart, int removedQty)>> RemoveLine(CartModel cart) =>
            TraceableTLifts.FromIOFin(
                IO.lift<Fin<(CartModel, int)>>(() =>
                {
                    var existing = cart.Items.Find(request.ProductId);
                    return existing.Match(
                        Some: line =>
                        {
                            var updatedItems = cart.Items.SetQuantity(request.ProductId, 0);
                            var updatedCart = cart with { Items = updatedItems };
                            return FinSucc((updatedCart, line.Quantity));
                        },
                        None: () => FinFail<(CartModel, int)>(Error.New("cart.product.not_in_cart"))
                    );
                }),
                spanName: "cart.remove_item.mutate");

        // main flow
        var flow = _cartService
            .GetCartForUser(request.UserId)
            .Bind(cartFin => cartFin.Match(
                Succ: cart => RemoveLine(cart)
                    .Bind(mutFin => mutFin.Match(
                        Succ: tuple => BuildResult(tuple.cart, tuple.removedQty),
                        Fail: error => TraceableTLifts.FromFin(Fin<RemoveItemFromCartDTO>.Fail(error), "cart.remove_item.failed", _ => []))),
                Fail: e => TraceableTLifts.FromFin(Fin<RemoveItemFromCartDTO>.Fail(e), "cart.fetch.fail", _ => [])))
            .WithSpanName("cart.remove_item.flow");

        return flow.RunTraceable(ct);
    }
}