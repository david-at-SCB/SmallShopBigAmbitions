using LanguageExt;
using LanguageExt.Traits;
using static LanguageExt.Prelude;
//using LanguageExt.Parallel;

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

// Fix for CS1061: Ensure the correct type is used for the `Match` method call.
// The error indicates that `Fin` does not have a `Match` method, but `Fin<T>` does.
// Update the code to use the generic `Fin<T>` type.

public static class IOFinLinqExtensions
{
    // Map over the inner Fin value
    public static IO<Fin<B>> Select<T, B>(this IO<Fin<T>> ma, Func<T, B> f) =>
        ma.Map(fin => fin.Map(f));

    // Bind with a function that returns IO<Fin<B>> (propagate Fin failures)
    public static IO<Fin<B>> SelectMany<T, B>(
        this IO<Fin<T>> ma,
        Func<T, IO<Fin<B>>> bind) =>
        ma.Bind(finT =>
            finT.Match(
                Succ: t => bind(t),
                Fail: error => IO<Fin<B>>.Pure(Fin<B>.Fail(error)) // Ensure proper type resolution
            )
        );

    // Bind + project (propagate Fin failures and project T,B -> C)
    public static IO<Fin<C>> SelectMany<T, B, C>(
    this IO<Fin<T>> ma,
    Func<T, IO<Fin<B>>> bind,
    Func<T, B, C> project) =>
    ma.Bind(finT =>
        finT.Match(
            Succ: t => bind(t).Map(finB =>
                finB.Match(
                    Succ: b => Fin<C>.Succ(project(t, b)),
                    Fail: error => Fin<C>.Fail(error)
                )
            ),
            Fail: error => IO<Fin<C>>.Pure(Fin<C>.Fail(error))
        )
    );
}


