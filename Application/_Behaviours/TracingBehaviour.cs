using SmallShopBigAmbitions.TracingSources;
using SmallShopBigAmbitions.FunctionalDispatcher;
using System.Diagnostics;
using SmallShopBigAmbitions.Auth;

namespace SmallShopBigAmbitions.Application._Behaviours;

public class TracingBehavior<TRequest, TResponse> : IFunctionalPipelineBehavior<TRequest, TResponse>
    where TRequest : IFunctionalRequest<TResponse>
{
    public IO<Fin<TResponse>> Handle(
        TRequest request,
        TrustedContext context,
        Func<TRequest, TrustedContext, CancellationToken, IO<Fin<TResponse>>> next,
        CancellationToken ct)
    {
        return IO<Fin<TResponse>>.Lift(() =>
        {
            // Start a new activity for tracing. This complements the existing tracing in TraceableT, with this being the entire pipeline's activity, and TraceableT are more granular.
            ct.ThrowIfCancellationRequested();
            using var activity = ShopActivitySource.Instance.StartActivity(typeof(TRequest).Name);

            var result = next(request, context, ct).Run();

            if (result.IsFail)
            {
                var err = result.Match(
                    Succ: _ => null,
                    Fail: e => e.Message
                );
                if (err is not null)
                    activity?.SetTag("error", err);
            }
            else
            {
                var val = result.Match(
                    Succ: v => v?.ToString(),
                    Fail: _ => null
                );
                if (val is not null)
                    activity?.SetTag("result", val);
            }

            return result;
        });
    }

}
