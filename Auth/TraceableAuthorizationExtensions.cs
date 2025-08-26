namespace SmallShopBigAmbitions.Auth;

using SmallShopBigAmbitions.Monads.TraceableTransformer;

public static class TraceableAuthorizationExtensions
{
    public static TraceableT<A> RequireTrusted<A>(this TraceableT<A> effect, TrustedContext context, string spanName = "RequireTrusted") =>
        TraceableAuthorizationGuards.RequireTrustedOrThrowTracableT(context, spanName)
            .Bind(_ => effect);

    public static TraceableT<A> EnsureTrusted<A>(this TraceableT<A> effect, TrustedContext context, string spanName = "EnsureTrusted") =>
        TraceableAuthorizationGuards.EnsureTrustedTracableT(context, spanName)
            .Bind(_ => effect);

    public static TraceableT<A> EnsureAdmin<A>(this TraceableT<A> effect, TrustedContext context, string spanName = "EnsureAdmin") =>
        TraceableAuthorizationGuards.EnsureAdminTracableT(context, spanName)
            .Bind(_ => effect);
}
