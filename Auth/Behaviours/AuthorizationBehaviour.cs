using SmallShopBigAmbitions.Auth.Policy;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Auth.Behaviours;

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
        return IO.lift<Fin<TResponse>>(() =>
        {
            foreach (var policy in _policies)
            {
                var result = policy.Authorize(request, context);
                if (result.IsFail)
                    return Fin<TResponse>.Fail((Error)result);
            }

            return next(request, context, ct).Run();
        });
    }
}

