namespace SmallShopBigAmbitions.Monads.TraceableTransformer;

public static class TraceableTAttributes
{
    public static Func<Option<T>, IEnumerable<KeyValuePair<string, object>>> FromOption<T>(string key) =>
        opt => new[]
        {
            new KeyValuePair<string, object>($"{key}.isSome", opt.IsSome),
            new KeyValuePair<string, object>($"{key}.value", opt.Match(Some: v => v?.ToString() ?? "null", None: () => "none"))
        };

    public static Func<Fin<T>, IEnumerable<KeyValuePair<string, object>>> FromFin<T>(string key) =>
        fin => new[]
        {
            new KeyValuePair<string, object>($"{key}.isSucc", fin.IsSucc),
            new KeyValuePair<string, object>($"{key}.value", fin.Match(Succ: v => v?.ToString() ?? "null", Fail: ex => $"fail: {ex.Message}"))
        };

    public static Func<Fin<Option<T>>, IEnumerable<KeyValuePair<string, object>>> FromFinOption<T>(string key) =>
        finOpt => new[]
        {
            new KeyValuePair<string, object>($"{key}.isSucc", finOpt.IsSucc),
            new KeyValuePair<string, object>($"{key}.isSome", finOpt.Match(Succ: opt => opt.IsSome, Fail: _ => false)),
            new KeyValuePair<string, object>($"{key}.value", finOpt.Match(
                Succ: opt => opt.Match(Some: v => v?.ToString() ?? "null", None: () => "none"),
                Fail: ex => $"fail: {ex.Message}"
            ))
        };

    public static Func<Option<Option<Fin<T>>>, IEnumerable<KeyValuePair<string, object>>> FromOptionOptionFin<T>(string key) =>
        nested => new[]
        {
            new KeyValuePair<string, object>($"{key}.outer.isSome", nested.IsSome),
            new KeyValuePair<string, object>($"{key}.inner.isSome", nested.Bind(x => x).IsSome),
            new KeyValuePair<string, object>($"{key}.inner.isSucc", nested.Bind(x => x).Match(Some: fin => fin.IsSucc, None: () => false)),
            new KeyValuePair<string, object>($"{key}.value", nested.Bind(x => x).Match(
                Some: fin => fin.Match(Succ: v => v?.ToString() ?? "null", Fail: ex => $"fail: {ex.Message}"),
                None: () => "none"
            ))
        };
}