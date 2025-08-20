using LanguageExt;
using MediatR;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using CartModel = SmallShopBigAmbitions.Business.Services.CartService.Cart;

namespace SmallShopBigAmbitions.Application.Cart;

public class GetCartForUserHandler : IFunctionalHandler<GetCartForUserQuery, CartModel>
{
    private readonly CartService _cartService;
    private readonly ILogger<GetCartForUserHandler> _logger;

    public GetCartForUserHandler(CartService cartService, ILogger<GetCartForUserHandler> logger)
    {
        _cartService = cartService;
        _logger = logger;
    }



    public IO<Fin<CartModel>> Handle(GetCartForUserQuery request, CancellationToken ct)
    {

        return _cartService.GetCartForUser(request.UserId)
        .WithSpanName("GetCartForUser")
        .WithLogging(_logger)
        .Map(cart =>
            cart == null
                ? Fin<CartModel>.Fail("Cart not found or empty")
                : Fin<CartModel>.Succ(cart)
        )
        .RunTraceable(ct); // returns IO<Fin<CartModel>>
    }
}
