using LanguageExt;

namespace SmallShopBigAmbitions.Monads;

/// <summary>
/// Lifts common monadic types into Traceable monad.
/// </summary>
public static class TraceableLifts
{
    public static Traceable<T> FromEffect<T>(
        Func<T> effect,
        string spanName,
        Func<T, IEnumerable<KeyValuePair<string, object>>>? attributes = null)
    {
        return new Traceable<T>(() => Task.FromResult(effect()), spanName, attributes);
    }

    public static Traceable<Fin<Option<T>>> FromIO<T>(
        IO<Task<Fin<Option<T>>>> io,
        string spanName,
        Func<Fin<Option<T>>, IEnumerable<KeyValuePair<string, object>>>? attributes = null)
    {
        return new Traceable<Fin<Option<T>>>(
            async () => await io.Run(),
            spanName,
            attributes
        );
    }

    public static Traceable<Fin<Option<T>>> FromIO<T>(
         IO<Task<Fin<Option<T>>>> io,
         string spanName)
    {
        return FromIO(io, spanName, null);
    }

    public static Traceable<Fin<T>> FromResult<T>(
        Fin<T> result,
        string spanName,
        Func<T, IEnumerable<KeyValuePair<string, object>>>? attributes = null)
    {
        return FromEffect(() => result, spanName, TraceableAttributes.FromResult(attributes));
    }

    public static Traceable<Option<T>> FromOption<T>(
        Option<T> option,
        string spanName,
        Func<T, IEnumerable<KeyValuePair<string, object>>>? attributes = null)
    {
        return FromEffect(() => option, spanName, TraceableAttributes.FromOption(attributes));
    }
}