using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.Monads.TraceableTransformer.Extensions.BaseLinq;

namespace SmallShopBigAmbitions.Application.Carts.GetCartForUser;

public class GetCartForUserHandler(CartService cartService) : IFunctionalHandler<GetCartForUserQuery, Models.Cart>
{
    private readonly CartService _cartService = cartService;

    public IO<Fin<Models.Cart>> Handle(GetCartForUserQuery request, TrustedContext context, CancellationToken ct)
    {
        var tracedCart =
            from auth in TraceableTLifts.FromIOFin(AuthorizationGuards.RequireTrustedFin(context), "auth.ensure_trusted")
            from cartFin in _cartService.GetCartForUser(request.UserId)
                .WithSpanName("cart.fetch_user")
            select cartFin;

        return tracedCart.RunTraceable(ct);
    }
}
