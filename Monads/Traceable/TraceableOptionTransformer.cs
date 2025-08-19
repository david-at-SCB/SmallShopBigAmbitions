using SmallShopBigAmbitions.Monads.Traceable;

namespace SmallShopBigAmbitions.Monads.Traceable;

[Obsolete("Use TraceableT instead. This monad is only used in logic examples")]
public readonly struct TraceableOptionT<T>
{
    private readonly Traceable<Fin<Option<T>>> _traceable;

    public TraceableOptionT(Traceable<Fin<Option<T>>> traceable) =>
        _traceable = traceable;

    public TraceableOptionT<U> Bind<U>(Func<T, TraceableOptionT<U>> f)
    {
        var traceable = _traceable; // capture the field, cant access instance '_traceable' in an anonymous lambda

        return new TraceableOptionT<U>(
            new Traceable<Fin<Option<U>>>(() =>
            {
                var result = traceable.RunTraceable();
                return result.Match(
                    Succ: opt => opt.Match(
                        Some: val => f(val)._traceable.RunTraceable(),
                        None: () => Fin<Option<U>>.Succ(Option<U>.None)
                    ),
                    Fail: err => Fin<Option<U>>.Fail(err)
                );
            }, _traceable.SpanName)
        );
    }

    public TraceableOptionT<U> Map<U>(Func<T, U> f, string spanName = "map")
    {
        var traceable = _traceable;  // capture the field, cant access instance '_traceable' in an anonymous lambda
        return new(new Traceable<Fin<Option<U>>>(() =>
        {
            var result = traceable.RunTraceable();
            return result.Map(opt => opt.Map(f));
        }, spanName));
    }

    public Fin<Option<T>> Run() => _traceable.RunTraceable();

    public static TraceableOptionT<T> Lift(Traceable<Fin<Option<T>>> traceable) =>
        new(traceable);
    public static TraceableOptionT<T> Some(T value, string spanName) =>
        new(new Traceable<Fin<Option<T>>>(() =>
            Fin<Option<T>>.Succ(Option<T>.Some(value)), spanName));

    public static TraceableOptionT<T> None(string spanName) =>
        new(new Traceable<Fin<Option<T>>>(() =>
            Fin<Option<T>>.Succ(Option<T>.None), spanName));

    public static TraceableOptionT<T> Fail(Error error, string spanName) =>
        new(new Traceable<Fin<Option<T>>>(() =>
            Fin<Option<T>>.Fail(error), spanName));

    public TraceableOptionT<T> WithLogging(ILogger logger) =>
        new(_traceable.WithLogging(logger));
}