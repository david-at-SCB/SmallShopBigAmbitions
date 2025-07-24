using LanguageExt;

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
        new(() => Task.FromResult(effect()), spanName, attributes);


    /// <summary>
    /// Lifts an asynchronous Aff<Fin<Option<T>>> effect into a Traceable monad.
    /// This is the most common case for async DB/API calls that may fail or return nothing.
    /// </summary>
    public static Traceable<Fin<Option<T>>> FromAsyncEffect<T>(
        Aff<Fin<Option<T>>> aff,
        string spanName,
        Func<Fin<Option<T>>, IEnumerable<KeyValuePair<string, object>>>? attributes = null) =>
        new(async () =>
        {
            var result = await aff.Run();
            return result.Match(
                Succ: value => value,
                Fail: error => Fin<Option<T>>.Fail(error)
            );
        }, spanName, attributes);


    /// <summary>
    /// Convenience overload for FromAsyncEffect without attributes.
    /// </summary>
    public static Traceable<Fin<Option<T>>> FromAff<T>(
        Aff<Fin<Option<T>>> aff,
        string spanName) =>
        FromAsyncEffect(aff, spanName, null);

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
     new(() => Task.FromResult(
         eff.Run().Match(
             Succ: value => value,
             Fail: error => Fin<Option<T>>.Fail(error)
         )
     ), spanName, attributes);

    /// <summary>
    /// Convenience overload of FromEff that lifts an Eff&lt;Fin&lt;Option&lt;T&gt;&gt;&gt; into a Traceable monad without span attributes.
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
}