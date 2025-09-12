namespace SmallShopBigAmbitions.Application._PipelineBehaviours;

using LanguageExt;
using LanguageExt.Common;
using SmallShopBigAmbitions.Auth;
using SmallShopBigAmbitions.FunctionalDispatcher;
using SmallShopBigAmbitions.Monads;
using SmallShopBigAmbitions.TracingSources;
using System.Diagnostics;

public sealed class ObservabilityBehavior<TRequest, TResponse>(
    ILogger<ObservabilityBehavior<TRequest, TResponse>> log
) : IFunctionalPipelineBehavior<TRequest, TResponse>
  where TRequest : IFunctionalRequest<TResponse>
{
    private static readonly ActivitySource activitySource = ShopActivitySource.Instance;

    public IO<Fin<TResponse>> Handle(
        TRequest request,
        TrustedContext context,
        Func<TRequest, TrustedContext, CancellationToken, IO<Fin<TResponse>>> next,
        CancellationToken ct)
    {
        var reqType = typeof(TRequest).Name;
        var enricher = request as ITraceEnrichment;
        var spanName = enricher?.GetSpanName() ?? reqType;

        // 1) Build an IO that *acquires* the span and returns an IDisposable
        IO<IDisposable> openSpan =
            IO<IDisposable>.Lift(() =>
            {
                var span = activitySource.StartActivity(spanName, ActivityKind.Internal);
                span?.SetTag("request.type", reqType);
                span?.SetTag("user.id", context.CallerId);

                if (enricher is not null)
                    foreach (var (k, v) in enricher.GetTraceAttributes())
                        span?.SetTag(k, v);

                return new SpanScope(span);
            });

        // 2) Compose: when evaluated, open the span, run next, log via taps, then dispose
        return openSpan.Bind(spanDisp =>
            next(request, context, ct)
                .TapSucc(_ => log.LogInformation("Handled {RequestType} for {CallerId}", reqType, context.CallerId))
                .TapFail(err =>
                {
                    log.LogError(err.ToException(), "Failed {RequestType} for {CallerId}: {Error}",
                        reqType, context.CallerId, err.Message);
                    Activity.Current?.SetTag("error.type", err.Message);
                })
                .Map(res =>
                {
                    spanDisp.Dispose();
                    return res;
                })
        );
    }
    private sealed class SpanScope : IDisposable
    {
        private readonly Activity? _activity;
        public SpanScope(Activity? activity) => _activity = activity;
        public void Dispose() => _activity?.Dispose();
    }
}

public interface ITraceEnrichment
{
    // Optional nicer span name for the request
    string? GetSpanName() => null;

    // Key–value attributes to attach to the span started by the behavior
    IEnumerable<(string Key, object? Value)> GetTraceAttributes();
}
