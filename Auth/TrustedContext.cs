namespace SmallShopBigAmbitions.Auth;

public record TrustedContext
{
    public Guid CallerId { get; init; } // Could be service ID or user ID
    public string Role { get; init; } = "Service"; // e.g., "Admin", "User", "Service"
    public string Token { get; init; } = string.Empty; // Optional JWT or API token
    public bool IsTrusted => Role == "Service" || Role == "Admin"; // Simplified trust logic

    public static IO<Unit> RequireTrusted(TrustedContext context) =>
    IO.lift(() => AuthorizationGuards.EnsureTrusted(context));

}
