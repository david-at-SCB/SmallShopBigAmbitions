// Async/IO checklist (Razor Pages + LanguageExt)
// - Handlers return Task/Task<IActionResult>; always await RunTraceable(ct).RunAsync().
// - Avoid .Run(), .Result, .Wait() on IO/Aff in web code.
// - Accept and pass CancellationToken to all effects/HTTP calls.
// - UI layer calls service async wrappers (Task<T>) and maps Fin/Option to IActionResult.
// - Use Fin/Option for errors; handle via Match in handlers and return proper IActionResult.
// - Keep heavy work in services; pages should be thin adapters.
// - Pass CancellationToken from handler to service.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Application.Billing.CheckoutUser;
using SmallShopBigAmbitions.Application.Cart;
using SmallShopBigAmbitions.Application.Cart.AddItemsAndCheckout;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Application.HelloWorld;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Logic_examples;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.TracingSources;
using System.Diagnostics;

namespace SmallShopBigAmbitions.Pages;

public class IndexModel : PageModel
{
    private readonly IFunctionalDispatcher _dispatcher;
    private readonly ActivitySource _activitySource = ShopActivitySource.Instance;

    public IndexModel(IFunctionalDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Option<Fin<Cart>> Cart { get; private set; }

    public Option<Fin<CheckoutUserResultDTO>> CheckoutResult { get; private set; }

    [BindProperty]
    public string? HelloResult { get; set; }

    [BindProperty]
    public string ResultMessage { get; private set; }

    [BindProperty]
    public Guid UserId { get; set; }
    public async Task OnGetAsync(Guid? userId, CancellationToken ct)
    {
        if (userId.HasValue)
        {
            var trustedContext = TrustedContextFactory.FromHttpContext(HttpContext);

            var result = await _dispatcher.Dispatch(new GetCartForUserQuery(userId.Value), ct).RunAsync();
            Cart = result.Match(
                Succ: cart => Option<Fin<Cart>>.Some(cart),
                Fail: err =>
                {
                    var failure = Fin<Cart>.Fail(err);
                    Cart = Option<Fin<Cart>>.Some(failure); // Fixed: Directly assign the failure to Cart
                }
            );
        }
        else
        {
            Cart = Option<Fin<Cart>>.None;
        }
    }

    public async Task<IActionResult> OnPostAddItemsAndCheckoutAsync(CancellationToken ct)
    {
        var callerId = Guid.NewGuid();
        var userId = UserId != Guid.Empty ? UserId : callerId;

        var trustedContext = TrustedContextFactory.FromHttpContext(HttpContext);

        var items = Cart.Match<Map<FakeStoreProduct, int>>(
            Some: finCart => finCart.Match(
                Succ: cart => cart.Items,
                Fail: _ => Map<FakeStoreProduct, int>()
            ),
            None: () => Map<FakeStoreProduct, int>()
        );

        var cmd = new AddItemsToCartCommand(
            userId,
            items
        );

        var result = await _dispatcher.Dispatch(cmd, ct).RunAsync();

        // Fix: Use the result of Some in a conditional statement
        CheckoutResult = result.Match(
            Succ: res =>
            {
                CheckoutResult = Option<Fin<CheckoutUserResultDTO>>.Some(res);
                return CheckoutResult;
            },
            Fail: err => Fin<CheckoutUserResultDTO>.Fail(err)
        );

        return Page();
    }

    public async Task<IActionResult> OnPostRunExampleAsync()
    {
        var result = TraceableIOLoggerExample.RunExample();
        ResultMessage = result;
        return Page();
    }


    public async Task<IActionResult> OnPostSayHelloAsync(string name)
    {
        // We wanna say hello to the world, or a specific name if provided. Lets use the appropriate request!
        var request = new HelloWorldRequest(name ?? "World");

        // Setup the traceable request. This is lazily evaluated, so it won't run until we call RunTraceable.
        var traceableRequest = TraceableTLifts.FromIO<HelloWorldRequest>(
            IO.lift(() => request),
            "HelloWorldRequest"
        );

        // Now we can use the dispatcher to run the request. Here we also trace the request but also the trip through the dispatcher
        var result = await traceableRequest
            .Bind(req => TraceableTLifts.FromIO<Fin<string>>(
                _dispatcher.Dispatch<string>(req, CancellationToken.None), // type mismatch! IO<Fin<string>> vs IO<string>
                "DispatchHelloWorld"
            ))
            .RunTraceable(CancellationToken.None)
            .RunAsync();

        // What did we get back? A Fin<string> with the result of the hello world request, or an Error if something went wrong.
        HelloResult = result.Match(
            Succ: msg => msg,
            Fail: err => $"Error: {err.Message}"
        );

        return Page();
    }

    public async Task<IActionResult> OnPostGoToProducts()
    {
        // how do I redirect to a different page in Razor Pages?
        // Redirect to the Products page
        return RedirectToPage("/Products");
    }

}