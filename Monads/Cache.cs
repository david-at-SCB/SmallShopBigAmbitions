namespace SmallShopBigAmbitions.Monads;

public class Cache<T>
{
    private readonly Dictionary<string, T> _cache = [];

    public T GetOrAdd(string key, Func<T> factory)
    {
        if (_cache.TryGetValue(key, out var value))
            return value;

        value = factory();
        _cache[key] = value;
        return value;
    }
}
