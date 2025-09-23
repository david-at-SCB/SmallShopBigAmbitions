using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Models;
using Microsoft.AspNetCore.Mvc;
using SmallShopBigAmbitions.Business.Services;

namespace SmallShopBigAmbitions.Pages;

public class CartModel : PageModel
{
    private readonly IFunctionalDispatcher _dispatcher;
    private readonly UserService _userService;
    public CartModel(IFunctionalDispatcher dispatcher, UserService userService)
    {
        _dispatcher = dispatcher;
        _userService = userService;
    }

    public Cart? Cart { get; private set; }
    public Money Subtotal { get; private set; } = new("SEK", 0);

    public async Task OnGetAsync(CancellationToken ct)
    {
        var (userId, authenticated, _) = _userService.EnsureUserId(HttpContext);
        if (userId != Guid.Empty)
        {
            var fin = await _dispatcher.Dispatch<GetCartForUserQuery, Cart>(new GetCartForUserQuery(userId), ct).RunAsync();
            _ = fin.Match(
                  Succ: c => { Cart = c; Subtotal = c.Total("SEK"); return unit; },
                  Fail: _ => unit);
        }
    }
}
