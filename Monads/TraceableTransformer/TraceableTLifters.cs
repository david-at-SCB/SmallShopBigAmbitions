using System.Diagnostics;

namespace SmallShopBigAmbitions.Monads.TraceableTransformer;

public static class TraceableTLifts
{
    /// <summary>
    /// Lift any IO<A> into a TraceableT<A> monad. This allows you to trace the execution of the IO operation, and this method creates a new span for the operation.
    /// </summary>
    /// <typeparam name="A"></typeparam>
    /// <param name="effect"> the IO itself</param>
    /// <param name="spanName">What shall we call the new span?</param>
    /// <param name="attributes">any metadata for the span. Kindly provide! Do yourself a favor :P</param>
    /// <returns></returns>
    public static TraceableT<A> FromIO<A>(
        IO<A> effect,
        string spanName,
        Func<A, IEnumerable<KeyValuePair<string, object>>>? attributes = null) =>
        new(effect, spanName, attributes);

    public static IO<Fin<A>> ToFinFromInnerSuccess<A>(this TraceableT<A> traceable, CancellationToken ct, Func<A, Fin<bool>> successSelector)
    {
        return traceable.RunTraceable(ct).Map(result =>
            successSelector(result).Match(
                Succ: _ => Fin<A>.Succ(result),
                Fail: err => Fin<A>.Fail(err)
            )
        );
    }

    public static IO<Fin<A>> RunTraceableFin<A>(this TraceableT<A> traceable, CancellationToken ct) =>
        IO<Fin<A>>.Lift(() =>
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                var result = traceable.RunTraceable().Run();
                return Fin<A>.Succ(result);
            }
            catch (Exception ex)
            {
                return Fin<A>.Fail(Error.New(ex));
            }
        });

    // Explicit names to avoid overload confusion, suffixed with TracableT
    public static TraceableT<A> FromIOFinThrowingTracableT<A>(
        IO<Fin<A>> effect,
        string spanName,
        Func<A, IEnumerable<KeyValuePair<string, object>>>? attributes = null) =>
        FromIO(
            IO.lift<A>(() => effect.Run().ThrowIfFail()),
            spanName,
            attributes
        );

    public static TraceableT<T> FromIO<T>(IO<T> io, string spanName) =>
        new(io, spanName);

    public static TraceableT<Fin<T>> FromIOFinRawTracableT<T>(IO<Fin<T>> io, string spanName) =>
        new(io, spanName);

    public static TraceableT<Fin<T>> FromFin<T>(Fin<T> fin, string spanName) =>
        new TraceableT<Fin<T>>(IO.lift<Fin<T>>(() => fin), spanName);

    public static TraceableT<T> FromValue<T>(T value, string spanName) =>
        new(IO.lift(() => value), spanName);
}