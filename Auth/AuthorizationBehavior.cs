using MediatR;

namespace SmallShopBigAmbitions.Auth;

public interface IAuthorizedRequest : IRequest
{
    TrustedContext Context { get; }
}

public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
where TRequest : IAuthorizedRequest
{
    public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var authResult = AuthorizationGuards.EnsureTrusted(request.Context);
        if (authResult.IsFail)
        {
            var error = authResult.Match(Succ: _ => Error.New("Unauthorized"), Fail: e => e);
            var fail = typeof(TResponse).GetMethod("Fail", new[] { typeof(Error) })?.Invoke(null, new object[] { error });
            return Task.FromResult((TResponse)fail!);
        }

        return next();
    }
}
