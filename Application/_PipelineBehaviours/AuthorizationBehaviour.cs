using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using System.Linq;

namespace SmallShopBigAmbitions.Application._Behaviours;

public class AuthorizationBehavior<TRequest, TResponse> : IFunctionalPipelineBehavior<TRequest, TResponse>
    where TRequest : IFunctionalRequest<TResponse>
{
    private readonly IEnumerable<IAuthorizationPolicy<TRequest>> _policies;

    public AuthorizationBehavior(IEnumerable<IAuthorizationPolicy<TRequest>> policies)
    {
        _policies = policies;
    }

    public IO<Fin<TResponse>> Handle(
        TRequest request,
        TrustedContext context,
        Func<TRequest, TrustedContext, CancellationToken, IO<Fin<TResponse>>> next,
        CancellationToken ct)
    {
        // Policies run against TrustedContext which is derived from ASP.NET authentication middleware
        return IO.lift<Fin<TResponse>>(() =>
        {
            var authFin = _policies
                .Select(p => p.Authorize(request, context))
                .Aggregate(
                    seed: Fin<Unit>.Succ(Unit.Default),
                    func: (acc, cur) => acc.Bind(_ => cur)
                );

            if (authFin.IsFail)
                return Fin<TResponse>.Fail((Error)authFin);

            return next(request, context, ct).Run();
        });
    }
}

