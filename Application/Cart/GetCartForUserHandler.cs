using MediatR;
using SmallShopBigAmbitions.Business.Services;
using CartModel = SmallShopBigAmbitions.Business.Services.CartService.Cart;

namespace SmallShopBigAmbitions.Application.Cart;

public class GetCartForUserHandler : IRequestHandler<GetCartForUserQuery, Fin<CartModel>>
{
    private readonly CartService _cartService;
    private readonly ILogger _logger;

    public GetCartForUserHandler(CartService cartService, ILogger logger)
    {
        _cartService = cartService;
        _logger = logger;
    }

    public async Task<Fin<CartModel>> Handle(GetCartForUserQuery request, CancellationToken ct)
    {
        var traceableCart =
            _cartService.GetCartForUser(request.UserId)
                .WithSpanName("GetCartForUser")
                .WithLogging(_logger)
                .Map(cart =>
                    cart == null
                        ? Fin<CartModel>.Fail("Cart not found or empty")
                        : Fin<CartModel>.Succ(cart)
                );

        return traceableCart.RunTraceable().Run();
    }
}