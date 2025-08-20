using LanguageExt;
using MediatR;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;

namespace SmallShopBigAmbitions.Auth;

public interface IAuthorizedRequest
{
    TrustedContext Context { get; }
}

public class AuthorizationBehavior<TRequest, TResponse> : IFunctionalPipelineBehavior<TRequest, TResponse>
    where TRequest : IAuthorizedRequest, IFunctionalRequest<TResponse>
{
    public IO<Fin<TResponse>> Handle(
        TRequest request,
        CancellationToken ct,
        Func<TRequest, CancellationToken, IO<Fin<TResponse>>> next)
    {
        var authResult = AuthorizationGuards.EnsureTrusted(request.Context);
        return authResult.Match(
            Succ: _ => next(request, ct),
            Fail: err => IO(Fin<TResponse>.Fail(err))
        );
    }
}

