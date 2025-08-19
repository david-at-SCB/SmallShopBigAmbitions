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
        new TraceableT<A>(effect, spanName, attributes);
}
