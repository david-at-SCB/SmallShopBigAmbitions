using SmallShopBigAmbitions.TracingSources;
using SmallShopBigAmbitions.FunctionalDispatcher;
using System.Diagnostics;
using SmallShopBigAmbitions.Auth;

namespace SmallShopBigAmbitions.Application._PipelineBehaviours;

public class TracingBehavior<TRequest, TResponse> : IFunctionalPipelineBehavior<TRequest, TResponse>
    where TRequest : IFunctionalRequest<TResponse>
{
    public IO<Fin<TResponse>> Handle(
        TRequest request,
        TrustedContext context,
        Func<TRequest, TrustedContext, CancellationToken, IO<Fin<TResponse>>> next,
        CancellationToken ct)
    {
        // Make this effect truly async to avoid blocking. Also record error/success attributes.
        return IO.liftAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            using var activity = ShopActivitySource.Instance.StartActivity(typeof(TRequest).Name, ActivityKind.Internal);

            var result = await next(request, context, ct).RunAsync();

            if (result.IsFail)
            {
                var err = result.Match(Succ: _ => null, Fail: e => e.Message);
                if (err is not null) activity?.SetTag("error", err);
            }
            else
            {
                var val = result.Match(Succ: v => v?.ToString(), Fail: _ => null);
                if (val is not null) activity?.SetTag("result", val);
            }

            return result;
        });
    }
}
