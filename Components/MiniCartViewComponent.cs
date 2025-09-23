using Microsoft.AspNetCore.Mvc;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.TracingSources;
using System.Diagnostics;
using SmallShopBigAmbitions.Business.Services;

namespace SmallShopBigAmbitions.Components;

/// <summary>
/// ViewComponent that renders the user's (or anonymous cookie user's) cart and emits tracing spans.
/// Spans:
///   ui.minicart.invoke     - overall render (tags: user.id, auth.mode, cart.items.count, error.message)
///   ui.minicart.get_cart   - child span around functional dispatcher call
/// </summary>
public sealed class MiniCartViewComponent : ViewComponent
{
    private static readonly ActivitySource ActivitySource = ShopActivitySource.Instance;
    private readonly IFunctionalDispatcher _dispatcher;
    private readonly UserService _userService;

    public MiniCartViewComponent(IFunctionalDispatcher dispatcher, UserService userService)
    { 
        _dispatcher = dispatcher; 
        _userService = userService; 
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        using var activity = ActivitySource.StartActivity("ui.minicart.invoke", ActivityKind.Internal);

        var (userId, isAuth, created) = _userService.EnsureUserId(HttpContext);
        activity?.SetTag("user.id", userId);
        activity?.SetTag("auth.mode", isAuth ? "authenticated" : created ? "anon.new" : "anon");

        Cart cart;
        try
        {
            cart = await LoadCartAsync(userId);
            activity?.SetTag("cart.items.count", cart.Items.Count);
        }
        catch (Exception ex)
        {
            activity?.SetTag("error", true);
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetTag("error.message", ex.Message);
            cart = Cart.Empty(userId == Guid.Empty ? Guid.NewGuid() : userId);
        }

        return View("Default", cart);
    }

    private async Task<Cart> LoadCartAsync(Guid userId)
    {
        if (userId == Guid.Empty) return Cart.Empty(Guid.NewGuid());
        using var span = ActivitySource.StartActivity("ui.minicart.get_cart", ActivityKind.Internal);
        var fin = await _dispatcher
            .Dispatch<GetCartForUserQuery, Cart>(new GetCartForUserQuery(userId), CancellationToken.None)
            .RunAsync();
        return fin.Match(
            Succ: c => c,
            Fail: e =>
            {
                span?.SetTag("error", true);
                span?.SetTag("error.message", e.Message);
                return Cart.Empty(userId);
            });
    }
}
