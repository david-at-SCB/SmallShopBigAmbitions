using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmallShopBigAmbitions.Application.Cart;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;

namespace SmallShopBigAmbitions.Business.Controllers;

[ApiController]
[Route("api/cart")]
public class CartController : ControllerBase
{
    private readonly IMediator _mediator;

    public CartController(IMediator mediator)
    {
        _mediator = mediator;
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

        Fin<CartService.Cart> result = await _mediator.Send(new GetCartForUserQuery(userId, trustedContext));

        return result.Match<IActionResult>(
            Succ: cart => Ok(cart),
            Fail: err => NotFound(new { error = err })
        );
    }
}



