using MediatR;
using SmallShopBigAmbitions.Business.Services;
using CartModel = SmallShopBigAmbitions.Business.Services.CartService.Cart;

namespace SmallShopBigAmbitions.Application.Cart;

public class GetCartForUserHandler : IRequestHandler<GetCartForUserQuery, Fin<CartModel>>
{
    private readonly CartService _cartService;

    public GetCartForUserHandler(CartService cartService)
    {
        _cartService = cartService;
    }

    public async Task<Fin<CCartModelart>> Handle(GetCartForUserQuery request, CancellationToken ct)
    {
        return await _cartService
            .GetCartForUser(request.UserId)
            .RunTraceable()
    }
}