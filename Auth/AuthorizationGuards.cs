using SmallShopBigAmbitions.Monads.TraceableTransformer;
using System.Diagnostics;

namespace SmallShopBigAmbitions.Auth;

public static class AuthorizationGuards
{
    public static Fin<Unit> EnsureAdmin(TrustedContext context) =>
        context.Role == "Admin"
            ? Fin<Unit>.Succ(Unit.Default)
            : Fin<Unit>.Fail("Unauthorized: Admin role required.");

    public static Fin<Unit> EnsureTrusted(TrustedContext context) =>
        context.IsAuthenticated
            ? Fin<Unit>.Succ(Unit.Default)
            : Fin<Unit>.Fail("Unauthorized: Caller is not authenticated.");

    public static Unit EnsureTrustedOrThrow(TrustedContext context)
    {
        if (!context.IsAuthenticated)
            throw new UnauthorizedAccessException("Untrusted context");
        return Unit.Default;
    }

    public static IO<Unit> RequireTrustedORThrow(TrustedContext context) =>
        IO.lift(() => EnsureTrusted(context).ThrowIfFail());

    public static IO<Fin<Unit>> RequireTrustedFin(TrustedContext context) =>
        IO.lift<Fin<Unit>>(() => EnsureTrusted(context));
}

public static class TraceableContextAttributes
{
    public static IEnumerable<KeyValuePair<string, object>> FromContext(TrustedContext context) =>
        new[]
        {
            new KeyValuePair<string, object>("UserId", context.CallerId),
            new KeyValuePair<string, object>("Role", context.Role),
            new KeyValuePair<string, object>("IsAuthenticated", context.IsAuthenticated),
            new KeyValuePair<string, object>("JwtId", context.JwtId ?? string.Empty),
            new KeyValuePair<string, object>("Issuer", context.Issuer ?? string.Empty),
            new KeyValuePair<string, object>("ExpiresAt", context.ExpiresAt?.ToString("O") ?? string.Empty)
        };
}
