using SmallShopBigAmbitions.Application._Policy;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Application._PipelineBehaviours;

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
            // Fail-fast: iterate policies in order; short-circuit on first failure to guarantee no further policy execution after first failure.
            // Avoid evaluating later policies when one fails, save unnecessary DB and other external calls.
            foreach (var policy in _policies)
            {
                var fin = policy.Authorize(request, context);
                if (fin.IsFail)
                {
                    return Fin<TResponse>.Fail((Error)fin);
                }
            }

            return next(request, context, ct).Run();
        });
    }
}

