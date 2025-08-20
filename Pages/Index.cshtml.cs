using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Application.Cart;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.Logic_examples;

namespace SmallShopBigAmbitions.Pages
{
    public class IndexModel : PageModel
    {
        private readonly IMediator _mediator;
        private readonly TraceableIOLoggerExample _loggerExample;

        public IndexModel(IMediator mediator, TraceableIOLoggerExample loggerExample)
        {
            _mediator = mediator;
            _loggerExample = loggerExample;
        }

        [BindProperty]
        public Guid UserId { get; set; }

        public Option<Fin<UserCheckoutResult>> CheckoutResult { get; private set; }
        public string ResultMessage { get; private set; }
        public Option<Fin<CartService.Cart>> Cart { get; private set; }

        public async Task OnGetAsync(Guid? userId, CancellationToken ct)
        {
            if (userId.HasValue)
            {
                var trustedContext = new TrustedContext
                {
                    CallerId = Guid.NewGuid(),
                    Role = "Service",
                    Token = Request.Headers.Authorization.ToString()
                };

                Cart = Some(await _mediator.Send(new GetCartForUserQuery(userId.Value, trustedContext), ct));
            }
            else
            {
                Cart = Option<Fin<CartService.Cart>>.None;
            }
        }

        public async Task<IActionResult> OnPostAddItemsAndCheckoutAsync_old(CancellationToken ct)
        {
            var trustedContext = new TrustedContext
            {
                CallerId = Guid.NewGuid(),
                Role = "Service",
                Token = Request.Headers.Authorization.ToString()
            };

            var cmd = new AddItemsAndCheckoutCommand(
                UserId != Guid.Empty ? UserId : Guid.NewGuid(),
                ["item1", "item2", "item3"],
                trustedContext);

            var result = await _mediator.Send(cmd, ct);
            CheckoutResult = Some(result);
            return Page();
        }
        public async Task<IActionResult> OnPostAddItemsAndCheckoutAsync(CancellationToken ct)
        {
            var callerId = Guid.NewGuid();
            var userId = UserId != Guid.Empty ? UserId : callerId;

            var token = Request.Headers.TryGetValue("Authorization", out var authHeader)
                ? authHeader.ToString()
                : string.Empty;

            var trustedContext = new TrustedContext
            {
                CallerId = callerId,
                Role = "Service",
                Token = token
            };

            var cmd = new AddItemsAndCheckoutCommand(
                userId,
                ["item1", "item2", "item3"],
                trustedContext);

            var result = await _mediator.Send(cmd, ct);
            CheckoutResult = Some(result);

            return Page();
        }


        public async Task<IActionResult> OnPostRunExampleAsync()
        {
            var result = TraceableIOLoggerExample.RunExample();
            ResultMessage = result;
            return Page();
        }
    }
}
