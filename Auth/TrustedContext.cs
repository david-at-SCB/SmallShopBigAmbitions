namespace SmallShopBigAmbitions.Auth;

public record TrustedContext
{
    public Guid CallerId { get; init; } // Could be service ID or user ID
    public string Role { get; init; } = "Anonymous"; // e.g., "Admin", "User", "Service", "Anonymous"

    // Derived from ASP.NET authentication middleware
    public bool IsAuthenticated { get; init; }

    // Non-sensitive JWT metadata for correlation (never the raw token)
    public string? JwtId { get; init; } // jti
    public string? Issuer { get; init; } // iss
    public DateTimeOffset? ExpiresAt { get; init; } // exp

    // Optional scopes/permissions (from "scope" claim or roles)
    public IReadOnlyCollection<string> Scopes { get; init; } = [];

    public static IO<Unit> RequireTrusted(TrustedContext context) =>
        IO.lift(() => AuthorizationGuards.EnsureTrusted(context));
}
