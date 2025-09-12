using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Application.Cart.GetCartForUser;
using SmallShopBigAmbitions.Models;
using Microsoft.AspNetCore.Mvc;

namespace SmallShopBigAmbitions.Pages;

public class CartModel : PageModel
{
    private readonly IFunctionalDispatcher _dispatcher;
    public CartModel(IFunctionalDispatcher dispatcher) => _dispatcher = dispatcher;

    public Cart? Cart { get; private set; }
    public Money Subtotal { get; private set; } = new("SEK", 0);

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (TryGetUserId(out var userId))
        {
            var fin = await _dispatcher.Dispatch<GetCartForUserQuery, Cart>(new GetCartForUserQuery(userId), ct).RunAsync();
            fin.Match(
                Succ: c => { Cart = c; Subtotal = c.Total("SEK"); return unit; },
                Fail: _ => unit);
        }
    }

    public async Task<PartialViewResult> OnGetMiniAsync(CancellationToken ct)
    {
        Cart? cart = null;
        if (TryGetUserId(out var userId))
        {
            var fin = await _dispatcher.Dispatch<GetCartForUserQuery, Cart>(new GetCartForUserQuery(userId), ct).RunAsync();
            cart = fin.Match(Succ: c => c, Fail: _ => null);
        }
        return Partial("/Shared/_MiniCart", cart ?? Cart.Empty(Guid.NewGuid()));
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        if (User?.Identity?.IsAuthenticated == true)
        {
            var idClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(idClaim, out userId)) return true;
        }
        if (Request.Cookies.TryGetValue("anon-id", out var raw) && Guid.TryParse(raw, out userId)) return true;
        return false;
    }
}
