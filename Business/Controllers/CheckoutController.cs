using Microsoft.AspNetCore.Mvc;
using SmallShopBigAmbitions.Application.Billing.CheckoutUser;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Controllers;

public record CheckoutRequest(Guid UserId);

[ApiController]
[Route("api/[controller]")]
public class CheckoutController : ControllerBase
{
    private readonly IFunctionalDispatcher _dispatcher;

    public CheckoutController(IFunctionalDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpPost]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, CancellationToken ct)
    {
        var trustedContext = TrustedContextFactory.FromHttpContext(HttpContext);

        var command = new CheckoutUserCommand(request.UserId);
        var result = await _dispatcher.Dispatch<CheckoutUserCommand, CheckoutUserResultDTO>(command, ct).RunAsync();

        return result.Match<IActionResult>(
            Succ: result => Ok(new
            {
                user = result.CustomerId,
                cart = result.Cart.Id,
                charged = result.Charged.Match(Succ: _ => true, Fail: _ => false)
            }),
            Fail: e => Problem(detail: e.Message)
        );
    }
}