using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Application.Cart;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;

namespace SmallShopBigAmbitions.Pages
{
    public class OrderModel : PageModel
    {
        private readonly IMediator _mediator;

        public OrderModel(IMediator mediator)
        {
            _mediator = mediator;
        }

        public Fin<CartService.Cart> Cart { get; private set; }

        public async Task OnGetAsync(Guid userId, CancellationToken ct)
        {
            var trustedContext = new TrustedContext
            {
                CallerId = Guid.NewGuid(),
                Role = "Service",
                Token = Request.Headers["Authorization"].ToString()
            };

            Cart = await _mediator.Send(new GetCartForUserQuery(userId, trustedContext), ct);
        }
    }
}
