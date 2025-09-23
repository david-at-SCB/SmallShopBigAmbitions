namespace SmallShopBigAmbitions.Application._PipelineBehaviours;

using LanguageExt;
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

        // 1) Build an IO that *acquires* a span when evaluated,
        //    returning an IDisposable that ends the span.
        IO<IDisposable> openSpan =
            IO<IDisposable>.Lift(() =>
            {
                var span = activitySource.StartActivity(spanName, ActivityKind.Internal);
                span?.SetTag("request.type", reqType);
                span?.SetTag("user.id", context.CallerId);

                if (enricher is not null)
                    foreach (var (key, value) in enricher.GetTraceAttributes())
                        span?.SetTag(key, value);

                return new SpanScope(span);
            });

        // 2) Compose: when evaluated, open the span, run next, log via taps, then add result attrs, then dispose
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
                    // Post-execution enrichment (result-based)
                    if (request is ITraceResultEnrichment<TResponse> post)
                    {
                        foreach (var (k, v) in post.GetResultAttributes(res))
                            Activity.Current?.SetTag(k, v);
                    }
                    spanDisp.Dispose();
                    return res;
                })
        );
    }

    private sealed class SpanScope(Activity? activity) : IDisposable
    {
        private readonly Activity? _activity = activity;

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

// New: optional result-stage enrichment
public interface ITraceResultEnrichment<TResponse> : ITraceEnrichment
{
    IEnumerable<(string Key, object? Value)> GetResultAttributes(Fin<TResponse> result);
}