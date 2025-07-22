//using FunctionalTracing;

//namespace SmallShopBigAmbitions.Logic_examples.FunctionalTracing;

//public class IO<T> : IMonad<T>
//{
//    private readonly Func<T> _effect;

//    public IO(Func<T> effect) => _effect = effect;

//    public T Run() => _effect();

//    public IO<TResult> Map<TResult>(Func<T, TResult> f) =>
//        new IO<TResult>(() => f(_effect()));

//    public IO<TResult> Bind<TResult>(Func<T, IO<TResult>> f) =>
//        new IO<TResult>(() => f(_effect()).Run());

//    public IMonad<TResult> Bind<TResult>(Func<T, IMonad<TResult>> binder)
//    {
//        throw new NotImplementedException();
//    }

//    IMonad<TResult> IMonad<T>.Map<TResult>(Func<T, TResult> mapper)
//    {
//        throw new NotImplementedException();
//    }

//    public IMonad<T> Filter(Func<T, bool> predicate)
//    {
//        throw new NotImplementedException();
//    }

//    public IMonad<TResult> Select<TResult>(Func<T, TResult> selector)
//    {
//        throw new NotImplementedException();
//    }

//    public IMonad<TSelect> SelectMany<TIntermediate, TSelect>(Func<T, IMonad<TIntermediate>> binder, Func<T, TIntermediate, TSelect> projector)
//    {
//        throw new NotImplementedException();
//    }
//}

//public static class IOExtensions
//{
//    public static IO<TResult> Select<T, TResult>(this IO<T> io, Func<T, TResult> selector) =>
//        io.Map(selector);

//    public static IO<TResult> SelectMany<T, TResult>(this IO<T> io, Func<T, IO<TResult>> binder) =>
//        io.Bind(binder);

//    public static IO<TSelect> SelectMany<T, TResult, TSelect>(
//        this IO<T> io,
//        Func<T, IO<TResult>> binder,
//        Func<T, TResult, TSelect> projector) =>
//        io.Bind(t => binder(t).Map(r => projector(t, r)));

//    public static IO<Result<T>> LogResult<T>(this IO<Result<T>> io, Logger logger) =>
//        io.Bind<Result<T>>(result => result.Match(
//            val => logger.Log($"✅ Success: {val}").Map(_ => Result<T>.Ok(val)),
//            err => logger.Log($"❌ Error: {err}").Map(_ => Result<T>.Fail(err))
//            ));
//}
