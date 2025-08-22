using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Pages
{
    public class ViewReportsModel : PageModel
    {
        private readonly IFunctionalDispatcher _dispatcher;

        public ViewReportsModel(IFunctionalDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        //public Fin<ReportService.Report> Reports { get; private set; }

        public async Task OnGetAsync(Guid userId, TrustedContext context, CancellationToken ct)
        {
            var cart = await _dispatcher.Dispatch(new GetCartForUserQuery(userId), ct).RunAsync();
            //Simulating a report generation
            // Reports = await _mediator.Send(new GetReportsQuery(userId, trustedContext), ct);
        }
    }
}