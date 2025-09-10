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
using SmallShopBigAmbitions.Application.Cart.AddItemToCart;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Application.HelloWorld;
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

    public Option<Fin<AddItemToCartDTO>> AddItemResult { get; private set; }

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
            var result = await _dispatcher.Dispatch<GetCartForUserQuery, Cart>(new GetCartForUserQuery(userId.Value), ct).RunAsync();
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

    // Demo handler: add a fixed product (id=1) with quantity 1 to the user's cart.
    public async Task<IActionResult> OnPostAddItemsAndCheckoutAsync(CancellationToken ct)
    {
        var callerId = Guid.NewGuid();
        var userId = UserId != Guid.Empty ? UserId : callerId;

        var qtyFin = Quantity.Create(1);
        if (qtyFin.IsFail)
        {
            var err = qtyFin.Match(Succ: _ => Error.New("unreachable"), Fail: e => e);
            ModelState.AddModelError(string.Empty, err.Message);
            AddItemResult = Option<Fin<AddItemToCartDTO>>.Some(Fin<AddItemToCartDTO>.Fail(err));
            return Page();
        }

        var qtyVal = qtyFin.Match(Succ: q => q, Fail: _ => default);

        var cmd = new AddItemToCartCommand(
            userId,
            new ExternalProductRef(1),
            qtyVal,
            PriceRef: new("SEK", 150), // TODO: realistic price from product service
            Source: "index.page");

        var result = await _dispatcher.Dispatch<AddItemToCartCommand, AddItemToCartDTO>(cmd, ct).RunAsync();

        AddItemResult = result.Match(
            Succ: dto => Option<Fin<AddItemToCartDTO>>.Some(dto),
            Fail: err => Option<Fin<AddItemToCartDTO>>.Some(Fin<AddItemToCartDTO>.Fail(err))
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
        var request = new HelloWorldRequest(name ?? "World");

        var traceableRequest = TraceableTLifts.FromIO<HelloWorldRequest>(
            IO.lift(() => request),
            "HelloWorldRequest"
        );

        var result = await traceableRequest
            .Bind(req => TraceableTLifts.FromIO<Fin<string>>(
                _dispatcher.Dispatch<HelloWorldRequest, string>(req, CancellationToken.None),
                "DispatchHelloWorld"
            ))
            .RunTraceable(CancellationToken.None)
            .RunAsync();

        HelloResult = result.Match(
            Succ: msg => msg,
            Fail: err => $"Error: {err.Message}"
        );

        return Page();
    }

    public async Task<IActionResult> OnPostGoToProducts()
    {
        return RedirectToPage("/Products");
    }
}