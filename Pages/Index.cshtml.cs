using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Application.Cart;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.Logic_examples;
using LanguageExt;

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

        public async Task<IActionResult> OnPostRunExampleAsync()
        {
            var result = TraceableIOLoggerExample.RunExample();
            ResultMessage = result;
            return Page();
        }
    }
}
