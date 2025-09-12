namespace SmallShopBigAmbitions.Monads;

using System;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

public static class IoFinTapExtensions
{
    // ---------- Action-based taps (no extra IO effects) ----------

    public static IO<Fin<T>> TapSucc<T>(this IO<Fin<T>> io, Action<T> onSucc) =>
        io.Map(fin =>
        {
            fin.Match(onSucc, _ => { /* ignore fail */ });
            return fin; // pass through
        });

    public static IO<Fin<T>> TapFail<T>(this IO<Fin<T>> io, Action<Error> onFail) =>
        io.Map(fin =>
        {
            fin.Match(_ => { /* ignore succ */ }, onFail);
            return fin; // pass through
        });

    // ---------- IO-based taps (compose additional IO effects) ----------

    public static IO<Fin<T>> TapSucc<T>(this IO<Fin<T>> io, Func<T, IO<Unit>> onSucc) =>
        io.Bind(fin =>
            fin.Match(
                Succ: v => onSucc(v).Map(_ => fin),
                Fail: _ => IO<Fin<T>>.Lift(() => fin)));

    public static IO<Fin<T>> TapFail<T>(this IO<Fin<T>> io, Func<Error, IO<Unit>> onFail) =>
        io.Bind(fin =>
            fin.Match(
                Succ: _ => IO<Fin<T>>.Lift(() => fin),
                Fail: e => onFail(e).Map(_ => fin)));

    // ---------- Optional: direct Fin<T> variants ----------

    public static Fin<T> TapSucc<T>(this Fin<T> fin, Action<T> onSucc)
    {
        fin.Match(onSucc, _ => { });
        return fin;
    }

    public static Fin<T> TapFail<T>(this Fin<T> fin, Action<Error> onFail)
    {
        fin.Match(_ => { }, onFail);
        return fin;
    }
}

