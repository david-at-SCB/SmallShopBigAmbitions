using SmallShopBigAmbitions.TracingSources;

namespace SmallShopBigAmbitions.Monads.TraceableTransformer;

public record TraceableT<A>(
    IO<A> Effect,
    string SpanName,
    Func<A, IEnumerable<KeyValuePair<string, object>>>? Attributes = null)
{
    /// <summary>
    /// Main method to run the traceable effect and log the span.
    /// The bread and butter of the traceable transformer.
    /// </summary>
    /// <returns></returns>
    public IO<A> RunTraceable() =>
        from result in Effect
        select LogSpan(result);

    /// <summary>
    /// CancellationToken-aware version of RunTraceable.
    /// </summary>
    /// <param name="ct"></param>
    /// <returns></returns>
    public IO<A> RunTraceable(CancellationToken ct) =>
        from result in IO.lift(() =>
        {
            ct.ThrowIfCancellationRequested();
            return Effect.Run();
        })
        select LogSpan(result);

    /// <summary>
    /// Do the actual logging of the span.
    /// Add attributes if provided.
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    private A LogSpan(A result)
    {
        using var activity = ShopActivitySource.Instance.StartActivity(SpanName);
        if (activity != null && Attributes != null)
        {
            foreach (var attr in Attributes(result))
                activity.SetTag(attr.Key, attr.Value);
        }
        return result;
    }

    public TraceableT<A> WithAttributes(Func<A, IEnumerable<KeyValuePair<string, object>>> newAttrs) =>
        this with { Attributes = newAttrs };

    public TraceableT<A> WithSpanName(string newSpanName) =>
        this with { SpanName = newSpanName };

    public TraceableT<A> WithLogging(ILogger logger)
    {
        IEnumerable<KeyValuePair<string, object>> combinedAttrs(A result)
        {
            var original = Attributes?.Invoke(result) ?? [];
            logger.LogInformation("TraceableT: {SpanName} result: {Result}", SpanName, result);
            return original;
        }

        return this with { Attributes = combinedAttrs };
    }

    public TraceableT<B> Map<B>(Func<A, B> f) =>
        new(
            Effect: IO.lift(() => f(Effect.Run())),
            SpanName: SpanName + ".Map",
            Attributes: b => Enumerable.Empty<KeyValuePair<string, object>>() // old syntax
        );

    public TraceableT<B> Bind<B>(Func<A, TraceableT<B>> f) =>
        new(
            Effect: IO.lift(() =>
            {
                var a = Effect.Run();
                var next = f(a);
                return next.Effect.Run();
            }),
            SpanName: SpanName + ".Bind",
            Attributes: b => [] // modern syntax
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

    public static TraceableT<Fin<T>> WithTracing<T>(
        string spanName,
        IO<Fin<T>> effect
        ) =>
        new(effect, spanName);

    public static TraceableT<Fin<B>> BindFin<A, B>(
        this TraceableT<Fin<A>> src,
        Func<A, TraceableT<Fin<B>>> onSucc,
        Func<Error, TraceableT<Fin<B>>>? onFail = null) =>
        src.Bind(fin => fin.Match(
            Succ: onSucc,
            Fail: e => onFail is null
                ? TraceableTLifts.FromFin(Fin<B>.Fail(e), src.SpanName + ".fail", _ => [])
                : onFail(e)
        ));
}