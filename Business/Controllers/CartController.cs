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
        var trustedContext = new TrustedContext
        {
            CallerId = Guid.NewGuid(), // Or extract from token
            Role = "Service", // Or "Admin", etc.
            Token = Request.Headers.Authorization.ToString()
        };

        var ct = new CancellationToken(); // Use a real CancellationToken in production. TODO:!
        Fin<CustomerCart> result = await _dispatcher.Dispatch(new GetCartForUserQuery(userId), ct).RunAsync();

        return result.Match<IActionResult>(
            Succ: cart => Ok(cart),
            Fail: err => NotFound(new { error = err })
        );
    }
}