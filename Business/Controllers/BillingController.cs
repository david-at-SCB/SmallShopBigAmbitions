using Microsoft.AspNetCore.Mvc;
using SmallShopBigAmbitions.Application.Billing.ChargeCustomer;
using SmallShopBigAmbitions.Application.Billing.Payments;
using SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Business.Controllers;

public record ChargeRequestDto(Guid CartId, Guid UserId);

[ApiController]
[Route("api/[controller]")]
public class BillingController(IFunctionalDispatcher dispatcher) : ControllerBase
{
    private readonly IFunctionalDispatcher _dispatcher = dispatcher;

    [HttpPost("charge")]
    public async Task<IActionResult> ChargeCustomer([FromBody] ChargeRequestDto request)
    {
        var trustedContext = TrustedContextFactory.FromHttpContext(HttpContext);
        var ct = HttpContext.RequestAborted;
        var command = new ChargeCustomerCommand(request.UserId, request.CartId);
        var result = await _dispatcher.Dispatch(command, ct).RunAsync();

        return result.Match<IActionResult>(
            Succ: r => Ok(r),
            Fail: err => Unauthorized(new { error = err.Message })
        );
    }

    [HttpGet("payment_intent")]
    public async Task<IActionResult> CreatePaymentIntent([FromQuery] Guid userId, [FromQuery] Guid cartId)
    {
        var trustedContext = TrustedContextFactory.FromHttpContext(HttpContext);
        var ct = HttpContext.RequestAborted;
        var query = new CreateIntentToPayCommand(userId, cartId));
        var result = await _dispatcher.Dispatch(query, ct).RunAsync();
        return result.Match<IActionResult>(
            Succ: intent => Ok(intent),
            Fail: err => BadRequest(new { error = err.Message })
        );
    }
}