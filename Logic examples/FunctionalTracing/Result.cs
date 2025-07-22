//namespace SmallShopBigAmbitions.Logic_examples.FunctionalTracing
//using global::FunctionalTracing;
//using Monads;


//    // Result<T> base
//    public abstract class Result<T> : IMonad<T>
//{
//    public abstract TResult Match<TResult>(Func<T, TResult> Success, Func<string, TResult> Failure);

//    public abstract IMonad<TResult> Bind<TResult>(Func<T, IMonad<TResult>> binder);

//    public IMonad<TResult> Map<TResult>(Func<T, TResult> mapper) =>
//        Match(
//            Success: value => Result<TResult>.Ok(mapper(value)),
//            Failure: error => Result<TResult>.Fail(error)
//        );

//    public IMonad<T> Filter(Func<T, bool> predicate) =>
//        Match(
//            Success: value => predicate(value) ? Result<T>.Ok(value) : Result<T>.Fail("Predicate not satisfied"),
//            Failure: error => Result<T>.Fail(error)
//        );

//    public IMonad<TResult> Select<TResult>(Func<T, TResult> selector) => Map(selector);

//    public IMonad<TSelect> SelectMany<TIntermediate, TSelect>(
//        Func<T, IMonad<TIntermediate>> binder,
//        Func<T, TIntermediate, TSelect> projector) =>
//        Bind(value => binder(value).Map(intermediate => projector(value, intermediate)));
//}
//}