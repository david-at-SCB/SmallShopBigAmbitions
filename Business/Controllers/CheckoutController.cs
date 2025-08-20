using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmallShopBigAmbitions.Application.Billing;

namespace SmallShopBigAmbitions.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CheckoutController : ControllerBase
{
    //private readonly IMediator _mediator;
    //private readonly ITrustedContextProvider _contextProvider;

    //public CheckoutController(IMediator mediator, ITrustedContextProvider contextProvider)
    //{
    //    _mediator = mediator;
    //    _contextProvider = contextProvider;
    //}

    //[HttpPost]
    //public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
    //{
    //    var context = _contextProvider.GetContext(HttpContext); // e.g., from JWT or headers
    //    var command = new ChargeCustomerCommand(request.CartId, request.UserId, context);

    //    var result = await _mediator.Send(command);

    //    return result.Match(
    //        Succ: charge => Ok(new { charge.ReceiptUrl, charge.Amount }),
    //        Fail: err => Problem(detail: err.Message)
    //    );
    //}
}
