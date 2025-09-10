// Async/IO checklist (Razor Pages + LanguageExt)
// - Handlers return Task/Task<IActionResult>; always await RunTraceable(ct).RunAsync().
// - Avoid .Run(), .Result, .Wait() on IO/Aff in web code.
// - Accept and pass CancellationToken to all effects/HTTP calls.
// - UI layer calls service async wrappers (Task<T>) and maps Fin/Option to IActionResult.
// - Use Fin/Option for errors; handle via Match and set Model state/data.
// - Keep heavy work in services; pages should be thin adapters.
// - Pass CancellationToken from handler to service.
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;

namespace SmallShopBigAmbitions.Pages
{
    public class ViewReportsModel : PageModel
    {
        private readonly IFunctionalDispatcher _dispatcher;

        public ViewReportsModel(IFunctionalDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public async Task OnGetAsync(Guid userId, CancellationToken ct)
        {
            var trustedContext = TrustedContextFactory.FromHttpContext(HttpContext);
            var cart = await _dispatcher.Dispatch<GetCartForUserQuery, Cart>(new GetCartForUserQuery(userId), ct).RunAsync();
        }
    }
}