using Microsoft.AspNetCore.Mvc;
using SmallShopBigAmbitions.Application._Abstractions;
using SmallShopBigAmbitions.Application.Billing.CheckoutUser;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Business.Controllers;

public record CheckoutRequest(Guid UserId, Cart Cart, Option<string> Currency);

[ApiController]
[Route("api/[controller]")]
public class CheckoutController(IFunctionalDispatcher dispatcher) : ControllerBase
{
    private readonly IFunctionalDispatcher _dispatcher = dispatcher;

    [HttpPost]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, CancellationToken ct)
    {
        var trustedContext = TrustedContextFactory.FromHttpContext(HttpContext);

        var userId = trustedContext.CallerId;
        var command = new GoToCheckoutUserCommand(userId, request.Cart, request.Currency);
        var result = await _dispatcher.Dispatch<GoToCheckoutUserCommand, CheckoutUserResultDTO>(command, ct).RunAsync();

        // the checkoutcommand should start the entire order of operations.
        // but from here we only direct to payment or confirmation based on result.
        var whichPage = result.Match(
            Succ: res => res.Validated.Success ? "confirmation" : "payment",
            Fail: _ => "error");
        

        return RedirectToPage(whichPage);
        //return result.Match<IActionResult>(
        //    Succ: result => Ok(new
        //    {
        //        user = result.CustomerId,
        //        cart = result.Cart.Id,
        //        charged = result.Charged.Match(Succ: _ => true, Fail: _ => false)
        //    }),
        //    Fail: e => Problem(detail: e.Message)
        //);
    }
}