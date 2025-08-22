namespace SmallShopBigAmbitions.Auth;

using LanguageExt;
using SmallShopBigAmbitions.Monads.TraceableTransformer;

public static class TraceableAuthorizationGuards
{
    public static TraceableT<Unit> RequireTrustedOrThrowTracableT(
        TrustedContext context,
        string spanName = "RequireTrusted"
    ) =>
        TraceableTLifts.FromIO<Unit>(
            AuthorizationGuards.RequireTrustedORThrow(context),
            spanName);

    public static TraceableT<Unit> EnsureAdminTracableT(
        TrustedContext context,
        string spanName = "EnsureAdmin"
    ) =>
        TraceableTLifts.FromIO<Unit>(
            IO.lift(() => AuthorizationGuards.EnsureAdmin(context).ThrowIfFail()),
            spanName
        );

    public static TraceableT<Unit> EnsureTrustedTracableT(
        TrustedContext context,
        string spanName = "EnsureTrusted"
    ) =>
        TraceableTLifts.FromIO<Unit>(
            IO.lift(() => AuthorizationGuards.EnsureTrusted(context).ThrowIfFail()),
            spanName    
        );

    public static TraceableT<Unit> RequireTrustedFinTracableT(
        TrustedContext context,
        string spanName = "RequireTrustedFin",
        Func<Unit, IEnumerable<KeyValuePair<string, object>>>? attributes = null
     ) =>
        TraceableTLifts.FromIO<Unit>(
            IO.lift(() => AuthorizationGuards.RequireTrustedFin(context).Run().ThrowIfFail()),
            spanName,
            attributes
        );
}