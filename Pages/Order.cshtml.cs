using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Pages
{
    public class OrderModel : PageModel
    {
        private readonly IFunctionalDispatcher _dispatcher;

        public OrderModel(IFunctionalDispatcher mediator)
        {
            _dispatcher = mediator;
        }

        public Fin<CustomerCart> Cart { get; private set; }

        public async Task OnGetAsync(Guid userId, CancellationToken ct)
        {
            var trustedContext = new TrustedContext
            {
                CallerId = Guid.NewGuid(),
                Role = "Service",
                Token = Request.Headers.Authorization.ToString()
            };

            Cart = await _dispatcher.Dispatch(new GetCartForUserQuery(userId), ct).RunAsync();
        }
    }
}