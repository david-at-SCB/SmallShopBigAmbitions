//using LanguageExt.TypeClasses;
//using static LanguageExt.Deriving;
//using static LanguageExt.Prelude;

//namespace SmallShopBigAmbitions.Monads.Traceable;

//public struct TraceableMonad : Monad<Traceable<T>, T>
//{
//    public static Traceable<T> Pure<T>(T value) =>
//        new(() => value, $"pure-{typeof(T).Name}");

//    public static Traceable<U> Bind<T, U>(Traceable<T> ma, Func<T, Traceable<U>> f) =>
//        new(() =>
//        {
//            var a = ma.RunTraceable();
//            var mb = f(a);
//            return mb.RunTraceable();
//        }, $"bind-{typeof(U).Name}");

//    public static Traceable<U> Map<T, U>(Traceable<T> ma, Func<T, U> f) =>
//        new(() =>
//        {
//            var a = ma.RunTraceable();
//            return f(a);
//        }, $"map-{typeof(U).Name}");
//}
