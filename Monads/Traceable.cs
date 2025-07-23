namespace SmallShopBigAmbitions.Monads;

using Microsoft.Extensions.Logging;
using System.Diagnostics;

public record class Traceable<T>(
    Func<Task<T>> Effect,
    string SpanName,
    Func<T, IEnumerable<KeyValuePair<string, object>>>? Attributes = null)
{
    public async Task<T> RunTraceableAsync()
    {
        using var activity = new Activity(SpanName).Start();
        var result = await Effect();
        if (activity != null && Attributes != null)
        {
            foreach (var attr in Attributes(result))
                activity.SetTag(attr.Key, attr.Value);
        }
        return result;
    }

    public Traceable<R> Map<R>(Func<T, R> f) =>
        new(async () => f(await RunTraceableAsync()), "", null);

    /// Creates a new Traceable monad by applying a function that returns another Traceable monad.
    /// This method ensures that each step in the chain defines its own span and attributes,
    /// adhering to best practices for tracing.
    /// </summary>
    /// <typeparam name="R">The type of the result produced by the next Traceable monad.</typeparam>
    /// <param name="f">A function that takes the result of the current Traceable and returns a new Traceable.</param>
    /// <returns>A new Traceable monad representing the next step in the chain.</returns>
    /// ✅ Best Practice:
    /// Let each monad define its own span and attributes.
    /// Do not reuse SpanName or Attributes across steps.
    /// </summary>
    /// <typeparam name="R"></typeparam>
    /// <param name="f"></param>
    /// <returns></returns>
    public Traceable<R> Bind<R>(Func<T, Traceable<R>> f) =>
        new(async () => await f(await RunTraceableAsync()).RunTraceableAsync(), "", null);

    public Traceable<T> Tap(Action<T> action) =>
        new(async () =>
        {
            var result = await RunTraceableAsync();
            action(result);
            return result;
        }, SpanName, Attributes);
}

public static class TraceableExtensions
{
    /// <summary>
    ///  Use to setup Serilog logging for traceable effects.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="traceable"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    public static Traceable<T> WithLogging<T>(this Traceable<T> traceable, Microsoft.Extensions.Logging.ILogger logger) =>
        new(async () =>
        {
            var result = await traceable.Effect();
            logger.LogInformation("Traceable [{Span}]: {@Result}", traceable.SpanName, result);
            return result;
        }, traceable.SpanName, traceable.Attributes);
}