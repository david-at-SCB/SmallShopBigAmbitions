using System.Diagnostics;

public record TraceableT<A>(
    IO<A> Effect,
    string SpanName,
    ActivitySource ActivitySource,
    Func<A, IEnumerable<KeyValuePair<string, object>>>? Attributes = null
)
{
    public IO<A> RunTraceable() =>
        from result in Effect
        select LogSpan(result);

    private A LogSpan(A result)
    {
        using var activity = ActivitySource.StartActivity(SpanName);
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
    new TraceableT<B>(
        Effect: IO.lift(() => f(Effect.Run())),
        SpanName: SpanName + ".Map",
        ActivitySource: ActivitySource,
        Attributes: b => Enumerable.Empty<KeyValuePair<string, object>>() // optional
    );

    public TraceableT<B> Bind<B>(Func<A, TraceableT<B>> f) =>
        new TraceableT<B>(
            Effect: IO.lift(() =>
            {
                var a = Effect.Run();
                var next = f(a);
                return next.Effect.Run();
            }),
            SpanName: SpanName + ".Bind",
            ActivitySource: ActivitySource,
            Attributes: b => Enumerable.Empty<KeyValuePair<string, object>>() // optional
        );
}