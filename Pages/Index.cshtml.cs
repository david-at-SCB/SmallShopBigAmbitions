// Async/IO checklist (Razor Pages + LanguageExt)
// - Handlers return Task/Task<IActionResult>; always await RunTraceable(ct).RunAsync().
// - Avoid .Run(), .Result, .Wait() on IO/Aff in web code.
// - Accept and pass CancellationToken to all effects/HTTP calls.
// - UI layer calls service async wrappers (Task<T>) and maps Fin/Option to IActionResult.
// - Use Fin/Option for errors; handle via Match in handlers and return proper IActionResult.
// - Keep heavy work in services; pages should be thin adapters.
// - Pass CancellationToken from handler to service.
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Application.Carts.AddItemToCart;
using SmallShopBigAmbitions.Application.HelloWorld;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Logic_examples;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using SmallShopBigAmbitions.TracingSources;
using System.Diagnostics;
using static LanguageExt.Prelude;

// Added for on-prem AD bootstrap auth
using System.Security.Claims;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;

namespace SmallShopBigAmbitions.Pages;

public class IndexModel : PageModel
{
    private readonly ActivitySource _activitySource = ShopActivitySource.Instance;
    private readonly IClaimsTransformation? _claimsTransformation;
    private readonly IFunctionalDispatcher _dispatcher;
    private readonly IDummyUserStore _dummyUsers;

    // optional injection
    private readonly UserService _userService;
    // inline login support

    public IndexModel(IFunctionalDispatcher dispatcher, UserService userService, IDummyUserStore dummyUsers, IClaimsTransformation? claimsTransformation = null)
    {
        _dispatcher = dispatcher;
        _userService = userService;
        _dummyUsers = dummyUsers;
        _claimsTransformation = claimsTransformation;
    }

    public Option<Fin<AddItemToCartResult>> AddItemResult { get; private set; }
    public Option<Fin<Cart>> Cart { get; private set; }
    [BindProperty] public string? HelloResult { get; set; }
    // Inline login fields (dev)
    [BindProperty] public string? LoginEmail { get; set; }

    [BindProperty] public string? LoginMessage { get; set; }
    [BindProperty] public string? LoginPassword { get; set; }
    [BindProperty] public string ResultMessage { get; private set; }
    [BindProperty] public Guid UserId { get; set; }
    public async Task OnGetAsync(Guid? userId, CancellationToken ct)
    {
        // --- Auto Negotiate attempt (moved from middleware) ---
        // If no authenticated user yet, try Windows (Negotiate) *once* here.
        if (!(User?.Identity?.IsAuthenticated ?? false))
        {
            var authResult = await HttpContext.AuthenticateAsync(NegotiateDefaults.AuthenticationScheme);
            if (authResult.Succeeded && authResult.Principal is not null)
            {
                // ClaimsTransformation already ran (IClaimsTransformation is invoked by AuthenticateAsync)
                // Add a display_name claim if missing (defensive; transformer already does this)
                var id = authResult.Principal.Identities.FirstOrDefault();
                if (id is not null && !id.HasClaim(c => c.Type == "display_name"))
                {
                    var dn = id.Name ?? authResult.Principal.Identity?.Name;
                    if (!string.IsNullOrWhiteSpace(dn))
                        id.AddClaim(new Claim("display_name", dn));
                }

                // Sign in under existing default cookie (DummyAuth) so the rest of the pipeline treats this as authenticated.
                await HttpContext.SignInAsync("DummyAuth", authResult.Principal, new AuthenticationProperties
                {
                    IsPersistent = false
                });

                // Update current request principal so layout shows it immediately.
                HttpContext.User = authResult.Principal;
            }
        }
        // --- End auto Negotiate attempt ---

        // Always ensure we have a stable UserId for the page (claim or anon cookie)
        UserId = _userService.EnsureUserId(HttpContext).userId;

        var fetchId = userId ?? UserId;
        if (fetchId != Guid.Empty)
        {
            var result = await _dispatcher
                .Dispatch<GetCartForUserQuery, Cart>(new GetCartForUserQuery(fetchId), ct)
                .RunAsync();

            Cart = result.Match(
                Succ: cart => Option<Fin<Cart>>.Some(cart),
                Fail: err => Option<Fin<Cart>>.Some(Fin<Cart>.Fail(err))
            );
        }
        else
        {
            Cart = Option<Fin<Cart>>.None;
        }
    }

    public async Task<IActionResult> OnPostGoToProducts()
    {
        await Task.Delay(199); // simulate some async work
        return RedirectToPage("/Products");
    }

    public async Task<IActionResult> OnPostRunExampleAsync()
    {
        var taskResult = Task.FromResult(TraceableIOLoggerExample.RunExample());
        var result = await taskResult;
        ResultMessage = result;
        return Page();
    }

    public async Task<IActionResult> OnPostSayHelloAsync(string name)
    {
        // build + dispatch under a single traceable span.
        var effectiveName = string.IsNullOrWhiteSpace(name) ? "World" : name;
        var request = new HelloWorldRequest(effectiveName);
        var (userId, isAuth, _) = _userService.EnsureUserId(HttpContext);

        // local helper to avoid repetition
        static KeyValuePair<string, object> KVP(string k, object v) => new(k, v ?? string.Empty);

        // Mole de olla, span with attributes based on success/failure
        var trace = TraceableTLifts.FromIO<Fin<string>>(
                _dispatcher.Dispatch<HelloWorldRequest, string>(request, CancellationToken.None),
                "helloworld.request")
            // additional attributes based on result, these are optional but very useful
            .WithAttributes(fin => fin.Match(
                Succ: msg => new[]
                {
                    KVP("helloworld.success", true),
                    KVP("helloworld.result.length", msg.Length),
                    KVP("request.name", effectiveName),
                    KVP("user.id", userId),
                    KVP("user.authenticated", isAuth)
                },
                Fail: e => new[]
                {
                    KVP("helloworld.success", false),
                    KVP("error.type", e.GetType().Name),
                    KVP("error.message", e.Message),
                    KVP("request.name", effectiveName),
                    KVP("user.id", userId),
                    KVP("user.authenticated", isAuth)
                }));

        // run the effect, get final result
        var fin = await trace
            .RunTraceable(CancellationToken.None)
            .RunAsync();

        // put the result message into a property for the page
        HelloResult = fin.Match(
            Succ: msg => msg,
            Fail: err => $"Error: {err.Message}" );

        return Page();
    }
}