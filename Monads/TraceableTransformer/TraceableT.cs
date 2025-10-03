using SmallShopBigAmbitions.TracingSources;
using System.Diagnostics;

namespace SmallShopBigAmbitions.Monads.TraceableTransformer;

public record TraceableT<A>(
    IO<A> Effect,
    string SpanName,
    Func<A, IEnumerable<KeyValuePair<string, object>>>? Attributes = null,
    Action<Activity, A>? Annotate = null) // NEW: richer than tags (status/events)
{
    public bool Success { get; internal set; }

    /// <summary>Run the effect inside a span and attach attributes (computed from the result) before disposing.</summary>
    public IO<A> RunTraceable() =>
        IO<A>.Lift(() =>
        {
            using var activity = ShopActivitySource.Instance.StartActivity(SpanName);
            // If there's no current ActivitySource subscriber, we still run the effect.
            var result = Effect.Run();

            if (activity is not null)
            {
                if (Attributes is not null)
                {
                    foreach (var kv in Attributes(result))
                        activity.SetTag(kv.Key, kv.Value);
                }

                // allow setting status, adding events, etc.
                Annotate?.Invoke(activity, result);
            }

            return result;
        });

    /// <summary>Cancellation-aware variant. (Checks CT before running; actual effect must honor CT itself.)</summary>
    public IO<A> RunTraceable(CancellationToken ct) =>
    IO<A>.Lift(() =>
    {
        ct.ThrowIfCancellationRequested();
        using var activity = ShopActivitySource.Instance.StartActivity(SpanName);
        var result = Effect.Run(); // ensure your effect observes ct if necessary

        if (activity is not null)
        {
            if (Attributes is not null)
            {
                foreach (var kv in Attributes(result))
                    activity.SetTag(kv.Key, kv.Value);
            }
            Annotate?.Invoke(activity, result);
        }

        return result;
    });


    /// <summary>Compose (don’t replace) attribute providers.</summary>
    public TraceableT<A> WithAttributes(Func<A, IEnumerable<KeyValuePair<string, object>>> add) =>
    this with
    {
        Attributes = Attributes is null
            ? add
            : (a => (Attributes(a) ?? Enumerable.Empty<KeyValuePair<string, object>>())
                        .Concat(add(a)))
    };

    /// <summary>Convenience for constant attributes.</summary>
    public TraceableT<A> WithAttributes(params (string Key, object? Value)[] attrs) =>
        WithAttributes(_ => attrs.Select(x => new KeyValuePair<string, object>(x.Key, x.Value ?? "")));

    public TraceableT<A> WithSpanName(string newSpanName) =>
        this with { SpanName = newSpanName };

    // ---------- Pure, lazy composition (keep SpanName; drop attrs by default because the result type changes) ----------

    public TraceableT<B> Map<B>(Func<A, B> f) =>
        new(
            Effect: Effect.Map(f),
            SpanName: SpanName,
            Attributes: null // result type changed; caller can add new attrs for B
        );

    /// <summary>
    /// Bind without creating a child span automatically.
    /// The entire composed chain will be covered by THIS TraceableT’s span when RunTraceable() is called.
    /// </summary>
    public TraceableT<B> Bind<B>(Func<A, TraceableT<B>> f) =>
        new(
            Effect: Effect.Bind(a => f(a).Effect), // do NOT run child here; keep lazy composition
            SpanName: SpanName,
            Attributes: null
        );
}

public static class TraceableTExtensions
{
    public static TraceableT<Fin<T>> WithTracingAndRetry<T>(
        string spanName,
        IO<Fin<T>> effect,
        int maxRetries,
        TimeSpan? delay = null
    ) =>
        new(
            Effect: RetryIO.WithRetry(effect, maxRetries, delay ?? TimeSpan.FromMilliseconds(200)),
            SpanName: spanName
        );

    /// <summary>
    /// Fin-aware bind: choose the next step based on Success/Failure.
    /// Use BindWithChildSpan if you want the chosen branch to appear as its own child span.
    /// </summary>
    public static TraceableT<Fin<B>> BindFin<A, B>(
        this TraceableT<Fin<A>> src,
        Func<A, TraceableT<Fin<B>>> onSucc,
        Func<Error, TraceableT<Fin<B>>>? onFail = null
    ) =>
        src.Bind(fin => fin.Match(
            Succ: onSucc,
            Fail: e => onFail is null
                ? TraceableTLifts.FromFin(Fin<B>.Fail(e), src.SpanName + ".fail", _ => System.Array.Empty<KeyValuePair<string, object>>())
                : onFail(e)
        ));
}
