using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Application._Policy;

/// <summary>
/// Require that the caller has one of the specified scopes.
/// Scopes are taken from TrustedContext.Scopes (e.g., OAuth2 "scope" claim).
/// </summary>
public class ScopePolicy<TRequest> : IAuthorizationPolicy<TRequest>
    where TRequest : IFunctionalRequest<object>
{
    private readonly System.Collections.Generic.HashSet<string> _required;

    public ScopePolicy(params string[] required)
    {
        _required = new System.Collections.Generic.HashSet<string>(required ?? System.Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
    }

    public Fin<Unit> Authorize(TRequest request, TrustedContext context)
    {
        if (!context.IsAuthenticated)
            return Fin<Unit>.Fail(Error.New("Unauthorized: not authenticated"));

        if (_required.Count == 0)
            return Fin<Unit>.Succ(Unit.Default);

        var hasAny = context.Scopes.Any(s => _required.Contains(s));
        return hasAny
            ? Fin<Unit>.Succ(Unit.Default)
            : Fin<Unit>.Fail(Error.New($"Forbidden: missing required scope(s): {string.Join(",", _required)}"));
    }
}
