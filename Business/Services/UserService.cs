// Async/IO checklist (Razor Pages + LanguageExt)
// - Handlers return Task/Task<IActionResult>; always await RunTraceable(ct).RunAsync().
// - Avoid .Run(), .Result, .Wait() on IO/Aff in web code.
// - Accept and pass CancellationToken to all effects/HTTP calls.
// - Services: expose composable TraceableT/IO; add Task wrappers (e.g., GetXAsync) for UI.
// - Use Fin/Option for errors; handle via Match in handlers and return proper IActionResult.
// - Map DTO -> domain in a mapper; keep IO layer DTO-focused.
// - Ensure DI registers FunctionalHttpClient and service; no static singletons.
// - Add .WithLogging and telemetry attributes where useful.
using SmallShopBigAmbitions.Database;
using SmallShopBigAmbitions.Models;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using System.Security.Claims;

namespace SmallShopBigAmbitions.Business.Services;

public class UserService(IDataAccess DataAccess)
{
    private readonly IDataAccess _dataAccess = DataAccess;
    private const string AnonymousCookie = "anon-id";

    public TraceableT<Fin<Customer>> GetUserById(Guid userId) =>
        _dataAccess.GetCustomerById(userId);

    public TraceableT<Fin<Customer>> GetUserById(Customer customer) =>
        _dataAccess.GetCustomerById(customer.Id);

    /// <summary>
    /// Resolve a stable caller/user id. Prefers authenticated NameIdentifier (or sub/oid via TrustedContextFactory logic),
    /// falls back to anon-id cookie (creates one if missing).
    /// Does not perform any persistence here beyond ensuring cookie. Purely for identity continuity.
    /// </summary>
    public (Guid userId, bool isAuthenticated, bool created) EnsureUserId(HttpContext http)
    {
        if (http.User?.Identity?.IsAuthenticated == true)
        {
            var raw = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(raw, out var gid))
                return (gid, true, false);
        }
        if (http.Request.Cookies.TryGetValue(AnonymousCookie, out var existing) && Guid.TryParse(existing, out var anon))
            return (anon, false, false);

        var newId = Guid.NewGuid();
        http.Response.Cookies.Append(AnonymousCookie, newId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddYears(1)
        });
        return (newId, false, true);
    }

    /// <summary>
    /// Convenience async wrapper (Task) for pages/components that just need the user id.
    /// </summary>
    public Task<Guid> EnsureUserIdAsync(HttpContext http) => Task.FromResult(EnsureUserId(http).userId);
}