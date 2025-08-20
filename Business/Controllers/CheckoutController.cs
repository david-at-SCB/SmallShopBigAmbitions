using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmallShopBigAmbitions.Application.Billing;
using SmallShopBigAmbitions.Auth;

namespace SmallShopBigAmbitions.Controllers;

public record CheckoutRequest(Guid UserId);

[ApiController]
[Route("api/[controller]")]
public class CheckoutController : ControllerBase
{
    private readonly IMediator _mediator;

    public CheckoutController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, CancellationToken ct)
    {
        var trustedContext = new TrustedContext
        {
            CallerId = Guid.NewGuid(),
            Role = "Service",
            Token = Request.Headers.Authorization.ToString()
        };

        var command = new CheckoutUserCommand(request.UserId, trustedContext);
        var result = await _mediator.Send(command, ct);

        return result.Match<IActionResult>(
            Succ: r => Ok(new
            {
                user = r.UserId,
                cart = r.CartId,
                charged = r.Charged.Match(Succ: _ => true, Fail: _ => false)
            }),
            Fail: e => Problem(detail: e.Message)
        );
    }
}
