using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Application.Billing.Payments; // CapturePaymentCommand
using SmallShopBigAmbitions.Application.Billing.Payments.CreateIntentToPay;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Application.Carts.AddItemToCart;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.Business.Services;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.TracingSources;
using System.Diagnostics;
using System.Security.Claims;
using SmallShopBigAmbitions.Application._Abstractions; // for PaymentMethod

namespace SmallShopBigAmbitions.Pages;

public class DemoModel : PageModel
{
    private readonly IFunctionalDispatcher _dispatcher;
    private readonly UserService _userService;
    private readonly IDummyUserStore _dummyUsers;
    private readonly ActivitySource _activity = ShopActivitySource.Instance;

    public DemoModel(IFunctionalDispatcher dispatcher, UserService userService, IDummyUserStore dummyUsers)
    {
        _dispatcher = dispatcher;
        _userService = userService;
        _dummyUsers = dummyUsers;
    }

    // State
    [BindProperty] public Guid UserId { get; set; }

    public Option<Fin<Cart>> Cart { get; private set; } = Option<Fin<Cart>>.None;
    public Option<Fin<AddItemToCartResult>> AddItemResult { get; private set; } = Option<Fin<AddItemToCartResult>>.None;
    public Option<Fin<IntentToPayDto>> PaymentIntentResult { get; private set; } = Option<Fin<IntentToPayDto>>.None;
    public Option<Fin<Unit>> CaptureResult { get; private set; } = Option<Fin<Unit>>.None;

    [BindProperty] public string? LoginEmail { get; set; }
    [BindProperty] public string? LoginPassword { get; set; }
    [BindProperty] public string? LoginMessage { get; set; }

    [BindProperty] public Guid? CurrentPaymentIntentId { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        UserId = _userService.EnsureUserId(HttpContext).userId;
        await LoadCart(UserId, ct);
    }

    // 1. Inline login (optional)
    public async Task<IActionResult> OnPostLoginAsync(CancellationToken ct)
    {
        using var act = _activity.StartActivity("demo.login", ActivityKind.Internal);
        if (string.IsNullOrWhiteSpace(LoginEmail) || string.IsNullOrWhiteSpace(LoginPassword))
        {
            LoginMessage = "Email + password required";
        }
        else if (_dummyUsers.ValidateCredentials(LoginEmail, LoginPassword, out var user) && user is not null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Email, user.Email),
                new("display_name", user.DisplayName),
                new("auth_kind", "password.demo")
            };
            foreach (var r in user.Roles) claims.Add(new Claim(ClaimTypes.Role, r));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "DummyAuth"));
            await HttpContext.SignInAsync("DummyAuth", principal, new AuthenticationProperties { IsPersistent = true });
            HttpContext.User = principal;
            LoginMessage = $"Logged in as {user.DisplayName}";
        }
        else
        {
            LoginMessage = "Invalid credentials";
        }
        UserId = _userService.EnsureUserId(HttpContext).userId;
        await LoadCart(UserId, ct);
        return Page();
    }

    // 2. Add item to cart
    public async Task<IActionResult> OnPostAddItemAsync(CancellationToken ct)
    {
        using var act = _activity.StartActivity("demo.add_item", ActivityKind.Internal);
        UserId = _userService.EnsureUserId(HttpContext).userId;
        var qFin = Quantity.Create(1);
        var cmd = qFin.IsSucc
            ? new AddItemToCartCommand(UserId, new SmallShopBigAmbitions.Models.ExternalProductRef(1), qFin.Match(q => q, _ => default), new Money("SEK", 150), "demo.page")
            : null;
        if (cmd is null)
        {
            AddItemResult = Option<Fin<AddItemToCartResult>>.Some(Fin<AddItemToCartResult>.Fail(Error.New("qty.invalid")));
        }
        else
        {
            var fin = await _dispatcher.Dispatch<AddItemToCartCommand, AddItemToCartResult>(cmd, ct).RunAsync();
            AddItemResult = Option<Fin<AddItemToCartResult>>.Some(fin);
        }
        await LoadCart(UserId, ct);
        return Page();
    }

    // 3. Create payment intent
    public async Task<IActionResult> OnPostCreateIntentAsync(CancellationToken ct)
    {
        using var act = _activity.StartActivity("demo.create_intent", ActivityKind.Internal);
        UserId = _userService.EnsureUserId(HttpContext).userId;
        // Need cart id from snapshot; load cart first
        await LoadCart(UserId, ct);
        var cartId = Cart.Match(
            Some: fin => fin.Match(Succ: c => c.Id, Fail: _ => Guid.Empty),
            None: () => Guid.Empty);
        if (cartId == Guid.Empty)
        {
            PaymentIntentResult = Option<Fin<IntentToPayDto>>.Some(Fin<IntentToPayDto>.Fail(Error.New("cart.missing")));
            return Page();
        }
        var meta = Map(("ui.flow", "demo"));
        var cmd = new IntentToPayCommand(
            CartId: cartId,
            Method: PaymentMethod.Card,
            Currency: "SEK",
            IdempotencyKey: null,
            ShippingAddress: null,
            Metadata: meta);
        var fin = await _dispatcher.Dispatch<IntentToPayCommand, IntentToPayDto>(cmd, ct).RunAsync();
        PaymentIntentResult = Option<Fin<IntentToPayDto>>.Some(fin);
        CurrentPaymentIntentId = fin.Match(Succ: dto => dto.PaymentIntentId, Fail: _ => CurrentPaymentIntentId);
        act?.SetTag("payment.intent.created", fin.IsSucc);
        return Page();
    }

    // 4. Capture intent (simulate completion)
    public async Task<IActionResult> OnPostCaptureAsync(Guid paymentIntentId, CancellationToken ct)
    {
        using var act = _activity.StartActivity("demo.capture_intent", ActivityKind.Internal);
        var cmd = new CapturePaymentCommand(paymentIntentId);
        var fin = await _dispatcher.Dispatch<CapturePaymentCommand, Unit>(cmd, ct).RunAsync();
        CaptureResult = Option<Fin<Unit>>.Some(fin);
        act?.SetTag("payment.capture.success", fin.IsSucc);
        return Page();
    }

    private async Task LoadCart(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty)
        {
            Cart = Option<Fin<Cart>>.None;
            return;
        }
        var cartResult = await _dispatcher.Dispatch<GetCartForUserQuery, Cart>(new GetCartForUserQuery(userId), ct).RunAsync();
        Cart = cartResult.Match(
            Succ: c => Option<Fin<Cart>>.Some(Fin<Cart>.Succ(c)),
            Fail: e => Option<Fin<Cart>>.Some(Fin<Cart>.Fail(e))
        );
    }
}