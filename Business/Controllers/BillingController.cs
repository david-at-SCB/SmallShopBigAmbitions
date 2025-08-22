using Microsoft.AspNetCore.Mvc;
using SmallShopBigAmbitions.Application.Billing.ChargeCustomer;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Business.Controllers;

public record ChargeRequestDto(Guid CartId, Guid UserId);

[ApiController]
[Route("api/[controller]")]
public class BillingController : ControllerBase
{
    private readonly IFunctionalDispatcher _dispatcher;

    public BillingController(IFunctionalDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    [HttpPost("charge")]
    public async Task<IActionResult> ChargeCustomer([FromBody] ChargeRequestDto request)
    {
        // Simulate extracting a TrustedContext from headers or token
        var trustedContext = new TrustedContext
        {
            CallerId = Guid.NewGuid(), // dummy, for now. TODO: Implement proper extraction
            Role = "Service",
            Token = Request.Headers.Authorization.ToString()
        };
        var ct = new CancellationToken(); // Use a real CancellationToken in production. TODO:!
        var command = new ChargeCustomerCommand(request.CartId, request.UserId);
        var result = await _dispatcher.Dispatch(command, ct).RunAsync();

        return result.Match<IActionResult>(
            Succ: r => Ok(r),
            Fail: err => Unauthorized(new { error = err })
        );
    }
}