using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Application.Billing;

namespace SmallShopBigAmbitions.Controllers;

public record ChargeRequestDto(Guid CartId, Guid UserId);

[ApiController]
[Route("api/[controller]")]
public class BillingController : ControllerBase
{
    private readonly IMediator _mediator;

    public BillingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("charge")]
    public async Task<IActionResult> ChargeCustomer([FromBody] ChargeRequestDto request)
    {
        // Simulate extracting a TrustedContext from headers or token
        var trustedContext = new TrustedContext
        {
            CallerId = Guid.NewGuid(), // Or extract from token
            Role = "Service", // Or "Admin", etc.
            Token = Request.Headers["Authorization"].ToString()
        };

        var command = new ChargeCustomerCommand(request.CartId, request.UserId, trustedContext);
        var result = await _mediator.Send(command);

        return result.Match<IActionResult>(
            Succ: r => Ok(r),
            Fail: err => Unauthorized(new { error = err })
        );
    }
}