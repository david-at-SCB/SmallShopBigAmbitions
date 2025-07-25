using LanguageExt;
using static LanguageExt.Prelude;
using LanguageExt.Parallel;

namespace SmallShopBigAmbitions.Monads.LanguageExtExtensions;

public static class MyFinExtensions
{
    public static Fin<T> Flatten<T>(this Fin<Fin<T>> fin)
        => fin.Match(
            Succ: inner => inner,
            Fail: e => Fin<T>.Fail(e)
        );
}

public static class MyOptionExtensions
{
    public static Option<R> Map<T1, T2, T3, T4, R>(
        Option<T1> o1, Option<T2> o2, Option<T3> o3, Option<T4> o4,
        Func<T1, T2, T3, T4, R> f) =>
        o1.Bind(t1 =>
        o2.Bind(t2 =>
        o3.Bind(t3 =>
        o4.Map(t4 => f(t1, t2, t3, t4)))));
}


