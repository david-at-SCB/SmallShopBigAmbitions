namespace SmallShopBigAmbitions.Monads.TraceableTransformer;

public static class TraceableTLinqExtensions
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
