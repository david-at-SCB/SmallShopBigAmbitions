using MediatR;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.Monads.TraceableTransformer;

namespace SmallShopBigAmbitions.Application.Cart;

public class AddItemsAndCheckoutHandler : IRequestHandler<AddItemsAndCheckoutCommand, Fin<UserCheckoutResult>>
{
    private readonly CartService _cartService;
    private readonly UserService _userService;
    private readonly ILogger<AddItemsAndCheckoutHandler> _logger;

    public AddItemsAndCheckoutHandler(CartService cartService, UserService userService, ILogger<AddItemsAndCheckoutHandler> logger)
    {
        _cartService = cartService;
        _userService = userService;
        _logger = logger;
    }

    public IO<Fin<UserCheckoutResult>> Handle(AddItemsAndCheckoutCommand request, CancellationToken ct)
    {
        var flow =
            from cart in _cartService.GetCartForUser(request.UserId)
            from updated in _cartService.AddItems(cart, request.Items)
            from result in _userService.CheckoutExistingCart(updated, request.UserId)
            select result;


        return flow
            .WithSpanName("AddItemsAndCheckout")
            .RunTraceable(ct)
            .Map(Fin<UserCheckoutResult>.Succ)
            .Catch(ex => IO(Fin<UserCheckoutResult>.Fail(Error.New(ex.Message)))); // IO errors CS1955

    }
}
