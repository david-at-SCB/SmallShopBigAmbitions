namespace SmallShopBigAmbitions.Monads;

using LanguageExt.Traits;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using static SmallShopBigAmbitions.Logic_examples.TraceableIOLoggerExample;

public record class Traceable<T>(
    Func<T> Effect,
    string SpanName,
    Func<T, IEnumerable<KeyValuePair<string, object>>>? Attributes = null)
{
    public T RunTraceable()
    {
        using var activity = new Activity(SpanName).Start();
        var result = Effect();
        if (activity != null && Attributes != null)
        {
            foreach (var attr in Attributes(result))
                activity.SetTag(attr.Key, attr.Value);
        }
        return result;
    }


    public static Traceable<ResultOpt<string>> FromIOWrapped(IO<Fin<Option<string>>> io, string span, Func<Option<string>, IEnumerable<KeyValuePair<string, object>>>? attrs = null)
        => new(() =>
        {
            var result = io.Run();
            return new ResultOpt<string>(result);
        }, span, result => attrs?.Invoke(result.Value.Match(Succ: x => x, Fail: _ => Option<string>.None)));


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
    public Traceable<R> Bind<R>(Func<T, Traceable<R>> f)
    {
        return new(() =>
        {
            var result = RunTraceable();
            return f(result).RunTraceable();
        }, SpanName, null); // return null so the next Traceable can define its own attributes
    }

    public Traceable<T> Tap(Action<T> action)
    {
        return new(() =>
        {
            var result = RunTraceable();
            action(result);
            return result;
        }, SpanName, Attributes);
    }
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
        new(() =>
        {
            var result = traceable.Effect();
            logger.LogInformation("Traceable [{Span}]: {@Result}", traceable.SpanName, result);
            return result;
        }, traceable.SpanName, traceable.Attributes);
}