using LanguageExt;
using LanguageExt.Common;
using SmallShopBigAmbitions.Monads.TraceableTransformer;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Application._Policy;

public static class PolicyTracingExtensions
{
    // Extract error codes from a semicolon-concatenated message using the documented pattern.
    private static IEnumerable<string> ExtractCodes(Error e)
    {
        var raw = e.Message ?? string.Empty;
        return raw
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Contains('.')); // heuristic: pattern segments
    }

    public static TraceableT<Fin<T>> WithErrorCodeTags<T>(this TraceableT<Fin<T>> traceable) =>
        traceable.WithAttributes(fin =>
        {
            var existing = traceable.Attributes?.Invoke(fin) ?? Enumerable.Empty<KeyValuePair<string, object>>();
            return fin.Match(
                Succ: _ => existing,
                Fail: e =>
                {
                    var codes = ExtractCodes(e).ToArray();
                    var list = new List<KeyValuePair<string, object>>(existing);
                    if (codes.Length > 0)
                    {
                        list.Add(new("error.code.primary", codes[0]));
                        list.Add(new("error.code.count", codes.Length));
                        list.Add(new("error.codes", string.Join(",", codes)));
                    }
                    else
                    {
                        list.Add(new("error.code.count", 0));
                    }
                    return list;
                });
        });
}