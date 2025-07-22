////using LanguageExt;
//using FunctionalTracing;

//namespace SmallShopBigAmbitions.Logic_examples.FunctionalTracing;

//// Option<T> base
//public abstract class Option<T> : IMonad<T>
//{
//    public abstract TResult Match<TResult>(Func<T, TResult> Some, Func<TResult> None);

//    public abstract IMonad<TResult> Bind<TResult>(Func<T, IMonad<TResult>> binder);

//    public IMonad<TResult> Map<TResult>(Func<T, TResult> mapper) =>
//        Match(
//            Some: value => Option.Some(mapper(value)) as IMonad<TResult>,
//            None: () => Option.None<TResult>()
//        );

//    public IMonad<T> Filter(Func<T, bool> predicate) =>
//        Match(
//            Some: value => predicate(value) ? Option.Some(value) as IMonad<T> : Option.None<T>(),
//            None: () => Option.None<T>()
//        );

//    public IMonad<TResult> Select<TResult>(Func<T, TResult> selector) => Map(selector);

//    public IMonad<TSelect> SelectMany<TIntermediate, TSelect>(
//        Func<T, IMonad<TIntermediate>> binder,
//        Func<T, TIntermediate, TSelect> projector) =>
//        Bind(value => binder(value).Map(intermediate => projector(value, intermediate)));
//}

//public static class Option
//{
//    public static Some<T> Some<T>(T value) => new Some<T>(value);

//    public static None<T> None<T>() => new None<T>();
//}

//public class Some<T>(T value) : Option<T>
//{
//    private readonly T _value = value;

//    public override TResult Match<TResult>(Func<T, TResult> Some, Func<TResult> None) =>
//        Some(_value);

//    public override IMonad<TResult> Bind<TResult>(Func<T, IMonad<TResult>> binder) =>
//        binder(_value);
//}

//public class None<T> : Option<T>
//{
//    public override TResult Match<TResult>(Func<T, TResult> Some, Func<TResult> None) =>
//        None();

//    public override IMonad<TResult> Bind<TResult>(Func<T, IMonad<TResult>> binder) =>
//        new None<TResult>();
//}