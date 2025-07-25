using static SmallShopBigAmbitions.Logic_examples.TraceableIOLoggerExample;

namespace SmallShopBigAmbitions.Monads;

/// <summary>
/// Provides helper methods to lift common functional monads (like Eff, Aff, Option, Fin)
/// into the Traceable<T> monad, which wraps computations with OpenTelemetry-compatible tracing spans.
///
/// This is useful when building pipelines that:
/// - Compose effectful operations (e.g., DB calls, API requests)
/// - Need to be traced and logged consistently
/// - Use LanguageExt monads like Option<T>, Fin<T>, Eff<T>, and Aff<T>
///
/// Each method here wraps a monadic value into a Traceable<T> by:
/// - Creating a named tracing span
/// - Optionally attaching attributes (tags) to the span
/// - Preserving the monadic structure (e.g., Fin<Option<T>>)
///
/// ✅ Best Practices:
/// - Use FromEff for synchronous effects (Eff<T>)
/// - Use FromAff for async effects (Aff<T>)
/// - Use FromResult or FromOption for pure values
/// - Always define a unique span name per operation
/// - Use TraceableAttributes to attach structured metadata
/// </summary>
public static class TraceableLifts
{
    /// <summary>
    /// Lifts a pure synchronous function into a Traceable monad.
    /// Useful for wrapping simple computations or pure values.
    /// </summary>
    public static Traceable<T> FromEffect<T>(
        Func<T> effect,
        string spanName,
        Func<T, IEnumerable<KeyValuePair<string, object>>>? attributes = null) =>
        new(() => effect(), spanName, attributes);

    /// <summary>
    /// Lifts an asynchronous IO<Fin<Option<T>>> effect into a Traceable monad.
    /// This is the most common case for async DB/API calls that may fail or return nothing.
    /// </summary>
    public static Traceable<Fin<Option<T>>> FromAsyncEffect<T>(
        IO<Fin<Option<T>>> io, 
        string spanName,
        Func<Fin<Option<T>>, IEnumerable<KeyValuePair<string, object>>>? attributes = null) =>
        new(() =>
        {
            var result = io.Run();
            return result.Match(
                Succ: value => value,
                Fail: error => Fin<Option<T>>.Fail(error)
            );
        }, spanName, attributes);

    public static Traceable<Fin<Option<T>>> FromIO<T>(
       IO<Fin<Option<T>>> io,
       string spanName,
       Func<Fin<Option<T>>, IEnumerable<KeyValuePair<string, object>>>? attributes = null) =>
      FromAsyncEffect(io, spanName, attributes);    

    /// <summary>
    /// Convenience overload for FromAsyncEffect without attributes.
    /// </summary>
    public static Traceable<Fin<Option<T>>> FromIO<T>(
        IO<Fin<Option<T>>> aff,
        string spanName) =>
        FromAsyncEffect(aff, spanName, null);

    public static Traceable<Fin<Seq<T>>> FromAffSeq_second<T>(
    IO<Fin<Seq<T>>> aff,
    string spanName,
    Func<Fin<Seq<T>>, IEnumerable<KeyValuePair<string, object>>>? attributes = null)
    {
        return new Traceable<Fin<Seq<T>>>(            
            () => aff.Run(),
            spanName,
            attributes
        );
    }

    public static Traceable<Fin<Seq<T>>> FromAffSeq<T>(
    IO<Fin<Seq<T>>> aff,
    string spanName,
    Func<Fin<Seq<T>>, IEnumerable<KeyValuePair<string, object>>>? attributes = null)
    {
        return new Traceable<Fin<Seq<T>>>(
            //() => aff.Run(), // CS0029 and CS1662. Now we have a ValueTask<Fin<Fin<Seq<T>>>
            //() => aff.Run().AsTask(), // CS0029 and CS1662. Now we have a Task<Fin<Fin<Seq<T>>>
            //() => aff.Run().FlatMap(finSucc), // maybe something like this? we need to bind/extract the result though
            //async () => await aff.Run(), // CS0266 and CS4010 cannot convert Fin<Fin<Seq<T>>> to Task<Fin<Seq<T>>>. Also, async lambdas cannot return anything but void, Task, or Task<T>
            () => FlattenAffFinSeq(aff), // this works, finally.
            spanName,
            attributes
        );
    }

    public static Fin<Seq<T>> FlattenAffFinSeq<T>(IO<Fin<Seq<T>>> aff)
    {
        var outer = aff.Run();
        return outer.Match(
            Succ: inner => inner,
            Fail: e => Fin<Seq<T>>.Fail(e)
        );
    }

    // the Traceable expects a Func<Task<T>> as the effect, but Aff<Fin<Seq<T>>> is not a Func<Task<T>>.
    // how do we massage our Aff to the correct type?
    // Traceable<T>(
    //    Func<Task<T>> Effect,
    //    string SpanName,
    //    Func<T, IEnumerable<KeyValuePair<string, object>>>? Attributes = null

    /// <summary>
    /// Internal helper that lifts an Eff&lt;Fin&lt;Option&lt;T&gt;&gt;&gt; into a Traceable monad.
    /// It executes the effect synchronously, wraps the result in a Task, and ensures proper error propagation.
    /// </summary>
    /// <typeparam name="T">The inner value type inside Option&lt;T&gt;.</typeparam>
    /// <param name="eff">The synchronous effect to lift.</param>
    /// <param name="spanName">The name of the tracing span.</param>
    /// <param name="attributes">Optional function to extract span attributes from the result.</param>
    /// <returns>A Traceable monad wrapping the effect and tracing metadata.</returns>
    private static Traceable<Fin<Option<T>>> FromEffInternal<T>(
     Eff<Fin<Option<T>>> eff,
     string spanName,
     Func<Fin<Option<T>>, IEnumerable<KeyValuePair<string, object>>>? attributes) =>
        new(() =>
        eff.Run().Match(
            Succ: value => value,
            Fail: error => Fin<Option<T>>.Fail(error)
            ), spanName, attributes);

    /// <summary>
    /// Convenience overload of FromEff that lifts an Eff<Fin<Option<T>>> into a Traceable monad without span attributes.
    /// </summary>
    /// <typeparam name="T">The inner value type inside Option&lt;T&gt;.</typeparam>
    /// <param name="eff">The synchronous effect to lift.</param>
    /// <param name="spanName">The name of the tracing span.</param>
    /// <returns>A Traceable monad wrapping the effect and tracing metadata.</returns>
    public static Traceable<Fin<Option<T>>> FromEff<T>(
        Eff<Fin<Option<T>>> eff,
        string spanName) =>
        FromEffInternal(eff, spanName, null);

    /// <summary>
    /// Lifts a synchronous Eff&lt;Fin&lt;Option&lt;T&gt;&gt;&gt; into a Traceable monad with optional span attributes.
    /// Use this when you want to trace a computation that may fail or return no value.
    /// </summary>
    /// <typeparam name="T">The inner value type inside Option&lt;T&gt;.</typeparam>
    /// <param name="eff">The synchronous effect to lift.</param>
    /// <param name="spanName">The name of the tracing span.</param>
    /// <param name="attributes">Optional function to extract span attributes from the result.</param>
    /// <returns>A Traceable monad wrapping the effect and tracing metadata.</returns>
    public static Traceable<Fin<Option<T>>> FromEff<T>(
        Eff<Fin<Option<T>>> eff,
        string spanName,
        Func<Fin<Option<T>>, IEnumerable<KeyValuePair<string, object>>>? attributes) =>
        FromEffInternal(eff, spanName, attributes);

    /// <summary>
    /// Lifts a pure Fin<T> result into a Traceable monad.
    /// </summary>
    public static Traceable<Fin<T>> FromResult<T>(
        Fin<T> result,
        string spanName,
        Func<T, IEnumerable<KeyValuePair<string, object>>>? attributes = null) =>
        FromEffect(() => result, spanName, TraceableAttributes.FromResult(attributes));

    /// <summary>
    /// Lifts a pure Option<T> value into a Traceable monad.
    /// </summary>
    public static Traceable<Option<T>> FromOption<T>(
        Option<T> option,
        string spanName,
        Func<T, IEnumerable<KeyValuePair<string, object>>>? attributes = null) =>
        FromEffect(() => option, spanName, TraceableAttributes.FromOption(attributes));

    public static Func<Fin<Seq<T>>, IEnumerable<KeyValuePair<string, object>>> FromResultSeq<T>(
        Func<Seq<T>, IEnumerable<KeyValuePair<string, object>>>? inner = null)
    {
        if (inner == null) return _ => Enumerable.Empty<KeyValuePair<string, object>>();

        return result => result.Match(
            Succ: value => inner(value),
            Fail: _ => Enumerable.Empty<KeyValuePair<string, object>>()
        );
    }


    public static Traceable<ResultOpt<T>> FromIO<T>(
        IO<Fin<Option<T>>> io,
        string span,
        Func<Option<T>, IEnumerable<KeyValuePair<string, object>>>? attrs = null)
        where T : notnull =>
        new(() =>
        {
            var result = io.Run();
            return new ResultOpt<T>(result);
        }, span, result => attrs?.Invoke(result.Value.Match(Succ: x => x, Fail: _ => Option<T>.None)));
}

