#pragma warning disable IDE0130 // needs separate namespace for linq extensions where SelectMany with same function signature exists
namespace SmallShopBigAmbitions.Monads.TraceableTransformer.Extensions.BaseLinq;
#pragma warning restore IDE0130

public static class BaseLinq
{
    public static TraceableT<B> Select<A, B>(this TraceableT<A> ta, Func<A, B> f) =>
        ta.Map(f);

    public static TraceableT<C> SelectMany<A, B, C>(
        this TraceableT<A> ta,
        Func<A, TraceableT<B>> bind,
        Func<A, B, C> project) =>
        ta.Bind(a => bind(a).Map(b => project(a, b)));

    public static TraceableT<B> SelectMany<A, B>(
        this TraceableT<A> ta,
        Func<A, TraceableT<B>> bind) =>
        ta.Bind(bind);
}


