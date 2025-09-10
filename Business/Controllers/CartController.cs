using Microsoft.AspNetCore.Mvc;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Business.Controllers;

[ApiController]
[Route("api/cart")]
public class CartController : ControllerBase
{
    private readonly IFunctionalDispatcher _dispatcher;

    public CartController(IFunctionalDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetCartForUser(Guid userId)
    {
        var trustedContext = TrustedContextFactory.FromHttpContext(HttpContext);

        var ct = HttpContext.RequestAborted;
        var query = new GetCartForUserQuery(userId);
        Fin<Cart> result = await _dispatcher.Dispatch<GetCartForUserQuery, Cart>(query, ct).RunAsync();

        return result.Match<IActionResult>(
            Succ: cart => Ok(cart),
            Fail: err => NotFound(new { error = err.Message })
        );
    }
}