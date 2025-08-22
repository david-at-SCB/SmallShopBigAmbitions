using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;

namespace SmallShopBigAmbitions.Application.Cart.GetCartForUser;

public class GetCartForUserHandler : IFunctionalHandler<GetCartForUserQuery, Models.CustomerCart>
{
    private readonly CartService _cartService;
    private readonly ILogger<GetCartForUserHandler> _logger;

    public GetCartForUserHandler(CartService cartService, ILogger<GetCartForUserHandler> logger)
    {
        _cartService = cartService;
        _logger = logger;
    }



    public IO<Fin<Models.CustomerCart>> Handle(GetCartForUserQuery request,TrustedContext context, CancellationToken ct)
    {
        return
            from _ in AuthorizationGuards.RequireTrustedORThrow(context) // RquireTrusted isnt recognized? CS0130
            from cart in _cartService.GetCartForUser(request.UserId)
            .WithSpanName("GetCartForUser")
            .WithLogging(_logger)
            //.Map(cart =>
            //     cart == null
            //     ? Fin<Models.Cart>.Fail("Cart not found or empty")
            //     : Fin<Models.Cart>.Succ(cart))
            .RunTraceableFin(ct)
            select cart;
    }

}
