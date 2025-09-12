using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Auth;
using System.Security.Claims;

namespace SmallShopBigAmbitions.Pages.Auth;

public class ImpersonateModel : PageModel
{
    private readonly IDummyUserStore _store;
    public ImpersonateModel(IDummyUserStore store) => _store = store;

    public IReadOnlyList<DummyUser> Users => _store.All;

    [TempData]
    public string? Message { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostLoginAsync(Guid id)
    {
        var user = _store.Get(id);
        if (user is null)
        {
            Message = "User not found.";
            return Page();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("display_name", user.DisplayName),
            new Claim("auth_kind", "dummy")
        };
        foreach (var r in user.Roles) claims.Add(new Claim(ClaimTypes.Role, r));

        var identity = new ClaimsIdentity(claims, "DummyAuth");
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync("DummyAuth", principal);
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync("DummyAuth");
        Message = "Signed out.";
        return RedirectToPage("/Index");
    }
}
