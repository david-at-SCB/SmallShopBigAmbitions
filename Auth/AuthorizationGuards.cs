namespace SmallShopBigAmbitions.Auth;

public static class AuthorizationGuards
{
    public static Fin<Unit> EnsureTrusted(TrustedContext ctx) =>
        ctx.IsTrusted
            ? Fin<Unit>.Succ(Unit.Default)
            : Fin<Unit>.Fail("Unauthorized: Caller is not trusted.");

    public static Fin<Unit> EnsureAdmin(TrustedContext ctx) =>
        ctx.Role == "Admin"
            ? Fin<Unit>.Succ(Unit.Default)
            : Fin<Unit>.Fail("Unauthorized: Admin role required.");
}
