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
using System.Diagnostics;

namespace SmallShopBigAmbitions.Pages;

public class IndexModel : PageModel
{
    private readonly IFunctionalDispatcher _dispatcher;
    private readonly ActivitySource _activitySource;

    public IndexModel(IFunctionalDispatcher dispatcher, ActivitySource activitySource)
    {
        _dispatcher = dispatcher;
        _activitySource = activitySource; // how do i get that ActivitySource injected? :D
    }

    public Option<Fin<CustomerCart>> Cart { get; private set; }

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
            var trustedContext = new TrustedContext
            {
                CallerId = Guid.NewGuid(),
                Role = "Service",
                Token = Request.Headers.Authorization.ToString()
            };

            var result = await _dispatcher.Dispatch(new GetCartForUserQuery(userId.Value), ct).RunAsync();
            Cart = result.Match(
                Succ: cart => Option<Fin<CustomerCart>>.Some(cart),
                Fail: err => Fin<CustomerCart>.Fail(err)
            );
        }
        else
        {
            Cart = Option<Fin<CustomerCart>>.None;
        }
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
            ["item1", "item2", "item3"] // Explicitly specify the type of the collection
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