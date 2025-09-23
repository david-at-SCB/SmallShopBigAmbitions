using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace SmallShopBigAmbitions.Pages.Dev;

public class ClaimsModel : PageModel
{
    private readonly IClaimsTransformation? _transformer;
    private readonly ILogger<ClaimsModel> _log;

    public ClaimsModel(ILogger<ClaimsModel> log, IClaimsTransformation? transformer = null)
    {
        _log = log;
        _transformer = transformer;
    }

    public string AuthType { get; private set; } = string.Empty;
    public bool IsAuthenticated { get; private set; }
    public string? Name { get; private set; }
    public int IdentityCount { get; private set; }
    public List<(string Type,string Value)> Claims { get; private set; } = new();
    public string Message { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken ct)
    {
        PopulateFromPrincipal(HttpContext.User);
    }

    public async Task<IActionResult> OnPostForceTransformAsync(CancellationToken ct)
    {
        // Attempt Negotiate auth and run transformer manually
        var authResult = await HttpContext.AuthenticateAsync(NegotiateDefaults.AuthenticationScheme);
        if (authResult.Succeeded && authResult.Principal is not null)
        {
            var principal = authResult.Principal;
            if (_transformer is not null)
            {
                principal = await _transformer.TransformAsync(principal);
                Message = "Claims transformer executed.";
            }
            else
            {
                Message = "No IClaimsTransformation registered.";
            }
            await HttpContext.SignInAsync("DummyAuth", principal, new AuthenticationProperties { IsPersistent = false });
            HttpContext.User = principal; // update for current request
            PopulateFromPrincipal(principal);
        }
        else
        {
            Message = "Negotiate authentication did not succeed.";
            PopulateFromPrincipal(HttpContext.User);
        }
        return Page();
    }

    private void PopulateFromPrincipal(ClaimsPrincipal principal)
    {
        var id = principal?.Identity;
        AuthType = id?.AuthenticationType ?? "(none)";
        IsAuthenticated = id?.IsAuthenticated ?? false;
        Name = id?.Name;
        IdentityCount = principal?.Identities.Count() ?? 0;
        Claims = principal?.Claims
            .OrderBy(c => c.Type)
            .Select(c => (c.Type, c.Value))
            .ToList() ?? new();
    }
}
