using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;

namespace SmallShopBigAmbitions.Application.Cart.GetCartForUser;

public class GetCartForUserHandler : IFunctionalHandler<GetCartForUserQuery, Models.Cart>
{
    private readonly CartService _cartService;
    private readonly ILogger<GetCartForUserHandler> _logger;

    public GetCartForUserHandler(CartService cartService, ILogger<GetCartForUserHandler> logger)
    {
        _cartService = cartService;
        _logger = logger;
    }

    public IO<Fin<Models.Cart>> Handle(GetCartForUserQuery request, TrustedContext context, CancellationToken ct)
    {
        var tracedCart =
            from auth in TraceableTLifts.FromIOFin(AuthorizationGuards.RequireTrustedFin(context), "auth.ensure_trusted")
            from cartFin in _cartService.GetCartForUser(request.UserId)
                .WithSpanName("cart.fetch_user")
                .WithLogging(_logger)
            select cartFin;

        return tracedCart.RunTraceable(ct);
    }
}
