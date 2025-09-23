using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SmallShopBigAmbitions.Auth;
using System.Security.Claims;
using SmallShopBigAmbitions.TracingSources;
using System.Diagnostics;

namespace SmallShopBigAmbitions.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly IDummyUserStore _users;
    private readonly ActivitySource _activity = ShopActivitySource.Instance;

    public LoginModel(IDummyUserStore users) => _users = users;

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    [TempData]
    public string? Message { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        using var act = _activity.StartActivity("auth.login.attempt", ActivityKind.Internal);
        act?.SetTag("auth.email.present", !string.IsNullOrWhiteSpace(Email));

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            Message = "Email and password required.";
            act?.SetTag("auth.success", false);
            return Page();
        }
        if (!_users.ValidateCredentials(Email, Password, out var user) || user is null)
        {
            Message = "Invalid credentials.";
            act?.SetTag("auth.success", false);
            act?.SetTag("auth.email", Email);
            return Page();
        }

        // Build claims principal
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("display_name", user.DisplayName),
            new Claim("auth_kind", "password"),
        };
        foreach (var r in user.Roles) claims.Add(new Claim(ClaimTypes.Role, r));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "DummyAuth"));
        await HttpContext.SignInAsync("DummyAuth", principal, new AuthenticationProperties { IsPersistent = true });

        act?.SetTag("auth.success", true);
        act?.SetTag("user.id", user.Id);
        act?.SetTag("user.roles", string.Join(',', user.Roles));

        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        using var act = _activity.StartActivity("auth.logout", ActivityKind.Internal);
        await HttpContext.SignOutAsync("DummyAuth");
        act?.SetTag("auth.logout", true);
        return RedirectToPage("/Index");
    }
}
