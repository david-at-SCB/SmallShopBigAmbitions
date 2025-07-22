namespace SmallShopBigAmbitions.Monads;

using LanguageExt;
using Serilog;
using System.Diagnostics;

public record class Traceable<T>(
        Func<T> Effect,
        string SpanName,
        Func<T, IEnumerable<KeyValuePair<string, object>>>? Attributes = null)
{
    public T Run()
    {
        using var activity = Trace.Source.StartActivity(SpanName, ActivityKind.Internal);
        var result = Effect();
        if (activity != null && Attributes != null)
        {
            foreach (var attr in Attributes(result))
                activity.SetTag(attr.Key, attr.Value);
        }
        return result;
    }

    public Traceable<R> Map<R>(Func<T, R> f) =>
        new(() => f(Run()), SpanName: "", Attributes: null);

    /// <summary>
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
          new(() =>
          {
              var t = this.Run();
              return f(t).Run();
          }, "", null);

    public Traceable<T> Tap(Action<T> action) =>
        new(() =>
        {
            var result = this.Run();
            action(result);
            return result;
        }, SpanName, Attributes);

    public static Traceable<IO<T>> FromIO(IO<T> io, string spanName, Func<T, IEnumerable<KeyValuePair<string, object>>>? attributes = null) =>
        new Traceable<IO<T>>(() => io, spanName, attributes);
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
    public static Traceable<T> WithLogging<T>(this Traceable<T> traceable, ILogger logger) =>
        new(() =>
        {
            var result = traceable.Run();
            logger.Information("Traceable result: {@Result}", result);
            return result;
        }, traceable.SpanName, traceable.Attributes);
}