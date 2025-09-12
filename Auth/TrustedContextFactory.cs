using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SmallShopBigAmbitions.Auth;

public static class TrustedContextFactory
{
    private const string AnonymousIdCookieName = "anon-id";

    public static TrustedContext FromHttpContext(HttpContext? httpContext)
    {
        var user = httpContext?.User;
        var isAuthed = user?.Identity?.IsAuthenticated == true;

        string? Find(string type) => user?.FindFirstValue(type);

        var sub = Find(ClaimTypes.NameIdentifier) ?? Find("sub") ?? Find("oid");
        var callerId = Guid.TryParse(sub, out var parsed) ? parsed : Guid.Empty;

        // Dummy auth detection
        var authKind = Find("auth_kind");
        var isDummy = authKind == "dummy";

        // If not authenticated, fall back to AnonymousId cookie as callerId
        if (!isAuthed && httpContext is not null && httpContext.Request.Cookies.TryGetValue(AnonymousIdCookieName, out var anon)
            && Guid.TryParse(anon, out var anonGuid))
        {
            callerId = anonGuid;
        }

        var role = Find(ClaimTypes.Role) ?? (isAuthed ? "User" : "Anonymous");

        // Optional non-sensitive JWT metadata
        var jti = Find("jti");
        var iss = Find("iss");
        DateTimeOffset? exp = null;
        var expStr = Find("exp");
        if (long.TryParse(expStr, out var seconds))
        {
            // exp is seconds since epoch per RFC7519
            exp = DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        var scopeStr = Find("scope");
        var scopes = new List<string>();
        if (!string.IsNullOrWhiteSpace(scopeStr)) scopes.AddRange(scopeStr.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (isDummy) scopes.Add("dummy");

        return new TrustedContext
        {
            CallerId = callerId,
            Role = role,
            IsAuthenticated = isAuthed || isDummy, // treat dummy as authenticated for lower-tier flows
            JwtId = jti,
            Issuer = iss,
            ExpiresAt = exp,
            Scopes = scopes
        };
    }
}
