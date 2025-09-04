using LanguageExt;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Monads.TraceableTransformer;

public static class TraceableTHelpers
{
    // Wrap a Fin<T> into a TraceableT<T> span, unwrapping success value or throwing on failure to keep type TraceableT<T>
    public static TraceableT<T> UnwrapFin<T>(Fin<T> fin, string spanName) =>
        TraceableTLifts.FromFinUnwrapped(fin, spanName, _ => []);

    // Create a TraceableT<T> from a Fin<T> with custom attributes
    public static TraceableT<T> FromFinUnwrapped<T>(
        Fin<T> fin,
        string spanName,
        Func<T, IEnumerable<KeyValuePair<string, object>>> attributes)
        => TraceableTLifts.FromFinUnwrapped(fin, spanName, attributes);

    // Convenience overload with empty attributes
    public static TraceableT<T> FromFinUnwrapped<T>(Fin<T> fin, string spanName) =>
        TraceableTLifts.FromFinUnwrapped(fin, spanName, _ => []);
}
