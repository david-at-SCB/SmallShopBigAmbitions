#pragma warning disable IDE0130 // needs separate namespace for linq extensions where SelectMany with same function signature exists
namespace SmallShopBigAmbitions.Monads.TraceableTransformer.Extensions.FinLinq;
#pragma warning restore IDE0130

public static class FinLinqExtensions
{
    public static TraceableT<Fin<TResult>> SelectMany<TSource, TIntermediate, TResult>(
            this TraceableT<Fin<TSource>> source,
            Func<TSource, TraceableT<Fin<TIntermediate>>> bind,
            Func<TSource, TIntermediate, TResult> project)
    {
        return source.BindFin(src =>
            bind(src).Map(fin =>
                fin.Map(inter => project(src, inter))
            )
        );
    }

    public static TraceableT<Fin<TResult>> Select<TSource, TResult>(
        this TraceableT<Fin<TSource>> source,
        Func<TSource, TResult> selector)
    {
        return source.Map(fin => fin.Map(selector));
    }

    public static TraceableT<Fin<TSource>> Where<TSource>(
        this TraceableT<Fin<TSource>> source,
        Func<TSource, bool> predicate)
    {
        return source.Map(fin =>
            fin.Match(
                Succ: val => predicate(val)
                    ? Fin<TSource>.Succ(val)
                    : Fin<TSource>.Fail(Error.New("Predicate failed")),
                Fail: err => Fin<TSource>.Fail(err)
            )
        );
    }
}