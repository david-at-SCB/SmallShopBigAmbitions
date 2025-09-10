using System.Collections.Immutable;

namespace SmallShopBigAmbitions.Monads.TraceableTransformer;

public static class TraceableTLifts
{
    public static TraceableT<Fin<T>> FromFin<T>(
            Fin<T> fin,
            string spanName,
            Func<Fin<T>, IEnumerable<KeyValuePair<string, object>>>? attributes) =>
                new(IO.lift<Fin<T>>(() => fin), spanName, attributes);

    /// <summary>
    /// Overload: accept HashMap attributes for Fin<T>.
    /// </summary>
    public static TraceableT<Fin<T>> FromFin<T>(
        Fin<T> fin,
        string spanName,
        Func<Fin<T>, HashMap<string, object>> attributes) =>
            new(IO.lift<Fin<T>>(() => fin), spanName, a => attributes(a));

    /// <summary>
    /// Overload: accept ImmutableDictionary attributes for Fin<T>.
    /// </summary>
    public static TraceableT<Fin<T>> FromFin<T>(
        Fin<T> fin,
        string spanName,
        Func<Fin<T>, ImmutableDictionary<string, object>> attributes) =>
            new(IO.lift<Fin<T>>(() => fin), spanName, a => attributes(a));

    public static TraceableT<T> FromFinUnwrapped<T>(
            Fin<T> fin,
            string spanName,
            Func<T, IEnumerable<KeyValuePair<string, object>>> attributes)
    {
        var unwrappedFinInNewTraceable = fin.Match<TraceableT<T>>(
            Succ: finValue =>
                            new TraceableT<T>(IO.lift(() => finValue), spanName, attributes),
            Fail: finError =>
                            new TraceableT<T>(IO.lift(() => FinFail<T>(finError).ThrowIfFail()), spanName, _ => []));

        return unwrappedFinInNewTraceable;
    }

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

    /// <summary>
    /// Overload: accept attributes as a LanguageExt.HashMap for unique-key semantics.
    /// </summary>
    public static TraceableT<A> FromIO<A>(
        IO<A> effect,
        string spanName,
        Func<A, HashMap<string, object>> attributes) =>
        new(effect, spanName, a => attributes(a));

   /// <summary>
   /// Overload: Create a new TraceableT without attributes
   /// </summary>
    public static TraceableT<T> FromIO<T>(IO<T> io, string spanName) =>
        new(io, spanName);

    public static TraceableT<Fin<T>> FromIOFin<T>(IO<Fin<T>> io, string spanName) =>
        new(io, spanName);

    // Explicit names to avoid overload confusion, suffixed with TracableT
    public static TraceableT<A> FromIOFinThrowing<A>(
        IO<Fin<A>> effect,
        string spanName,
        Func<A, IEnumerable<KeyValuePair<string, object>>>? attributes = null) =>
        FromIO(
            IO.lift<A>(() => effect.Run().ThrowIfFail()),
            spanName,
            attributes
        );

    public static TraceableT<T> FromValue<T>(T value, string spanName) =>
        new(IO.lift(() => value), spanName);

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
}