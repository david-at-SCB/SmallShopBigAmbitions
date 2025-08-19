namespace SmallShopBigAmbitions.Monads.Traceable;

public static class TraceableAttributes
{
    public static Func<T, IEnumerable<KeyValuePair<string, object>>> FromValue<T>(string key)
    where T : notnull
    {
        return value => [new KeyValuePair<string, object>(key, ConvertToOtelType(value))];
    }

    /// <summary>
    /// OTel expects attributes to be of type string, bool, int, long, float, double or arrays of these types.
    /// This helper method converts a value of any type to one of these types.
    /// It falls back to string representation if the type is not one of the expected types.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="value"></param>
    /// <returns></returns>
    private static object ConvertToOtelType<T>(T value)
    {
        return value switch
        {
            string s => s,
            bool b => b,
            int i => i,
            long l => l,
            float f => f,
            double d => d,
            string[] sa => sa,
            bool[] ba => ba,
            int[] ia => ia,
            long[] la => la,
            float[] fa => fa,
            double[] da => da,
            _ => value?.ToString() ?? string.Empty // fallback to string
        };
    }

    public static Func<Fin<T>, IEnumerable<KeyValuePair<string, object>>> FromResult<T>(string key)
    {
        return result => result.Match(
            //Succ: value => new[] { KeyValuePair.Create(key, (object)value!) },
            Succ: value => [new KeyValuePair<string, object>(key, ConvertToOtelType(value))],
            Fail: _ => Enumerable.Empty<KeyValuePair<string, object>>()
        );
    }

    // old and wrong. but why?
    //public static Func<Fin<T>> FromResultSeq<T>(
    //    Func<Aff<Fin<Seq<T>>>, IEnumerable<KeyValuePair<string, object>>> function)
    //{
    //    return result => result.Match( // => gives error CS1593 
    //        Succ: value => [new KeyValuePair<string, object>(function, ConvertToOtelType(value))],
    //        Fail: _ => Enumerable.Empty<KeyValuePair<string, object>>());
    //}
    public static Func<Fin<Seq<T>>, IEnumerable<KeyValuePair<string, object>>> FromResultSeq<T>(
    Func<Seq<T>, IEnumerable<KeyValuePair<string, object>>> inner)
    {
        return result => result.Match(
            Succ: value => inner(value),
            Fail: _ => Enumerable.Empty<KeyValuePair<string, object>>()
        );
    }



    public static Func<Fin<T>, IEnumerable<KeyValuePair<string, object>>>? FromResult<T>(
    Func<T, IEnumerable<KeyValuePair<string, object>>>? inner)
    {
        if (inner == null) return null;

        return result => result.Match(
            Succ: value => inner(value),
            Fail: _ => []);
    }


    public static Func<Option<T>, IEnumerable<KeyValuePair<string, object>>>? FromOption<T>(
        Func<T, IEnumerable<KeyValuePair<string, object>>>? inner)
    {
        if (inner == null) return null;

        return option => option.Match(
            Some: value => inner(value),
            None: () => Enumerable.Empty<KeyValuePair<string, object>>());
    }


    public static Func<Option<T>, IEnumerable<KeyValuePair<string, object>>> FromOption<T>(string key)
    {
        return option => option.Match(
            Some: value => [new KeyValuePair<string, object>(key, ConvertToOtelType(value))],
            None: () => Enumerable.Empty<KeyValuePair<string, object>>()
        );
    }

    public static Func<Fin<Option<T>>, IEnumerable<KeyValuePair<string, object>>> FromResultOption<T>(string key)
    {
        return result => result.Match(
            Succ: opt => opt.Match(
                Some: value => [new KeyValuePair<string, object>(key, ConvertToOtelType(value))],
                None: () => Enumerable.Empty<KeyValuePair<string, object>>()
            ),
            Fail: _ => []
        );
    }
}