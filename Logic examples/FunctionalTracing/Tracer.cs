//using LanguageExt.TypeClasses;
//using System.Diagnostics;

//namespace SmallShopBigAmbitions.Logic_examples.FunctionalTracing;


//// Tracer setup
//public static class MyTracer
//{
//    public static readonly ActivitySource Source = new("MyApp.Tracing");
//}

//// 3. TracedOption<T> monad
//public class TracedOption<T>
//{
//    private readonly Option<T> _option;
//    private readonly string _spanName;

//    public TracedOption(Option<T> option, string spanName)
//    {
//        _option = option;
//        _spanName = spanName;
//    }

//    public TracedOption<TResult> Bind<TResult>(Func<T, TracedOption<TResult>> binder)
//    {
//        return _option.Match(
//            Some: val =>
//            {
//                using var activity = MyTracer.Source.StartActivity(_spanName, ActivityKind.Internal);
//                return binder(val);
//            },
//            None: () => new TracedOption<TResult>(Option.None<TResult>(), _spanName));
//    }

//    public Option<T> Unwrap() => _option;
//}



//// Tap and Trace functions
//public static class TapAndTraceFunctions
//{
//    // Using Match
//    public static Option<T> TapAndTrace<T>(
//        this Option<T> option,
//        string spanName,
//        Func<T, IEnumerable<KeyValuePair<string, object>>>? attributes = null)
//    {
//        return option.Match(
//            Some: val =>
//            {
//                using var activity = MyTracer.Source.StartActivity(spanName, ActivityKind.Internal);
//                if (activity != null && attributes != null)
//                {
//                    foreach (var attr in attributes(val))
//                        activity.SetTag(attr.Key, attr.Value);
//                }
//                return option;
//            },
//            None: () => option);
//    }

//    // Using Monad.Return. Also depend on interface
//    public static IMonad<T> TapAndTrace<T>(
//      this IMonad<T> monad,
//      string spanName,
//      Func<T, IEnumerable<KeyValuePair<string, object>>>? attributes = null)
//    {
//        return monad.Bind(value =>
//        {
//            using var activity = MyTracer.Source.StartActivity(spanName, ActivityKind.Internal);
//            if (activity != null && attributes != null)
//            {
//                foreach (var attr in attributes(value))
//                    activity.SetTag(attr.Key, attr.Value);
//            }
//            return Monad.Return(value);
//        });
//    }
//}


//public class Traced<T> : IMonad<T>
//{
//    private readonly T _value;
//    private readonly string _spanName;

//    public Traced(T value, string spanName)
//    {
//        _value = value;
//        _spanName = spanName;
//    }

//    public IMonad<TResult> Bind<TResult>(Func<T, IMonad<TResult>> binder)
//    {
//        using var activity = MyTracer.Source.StartActivity(_spanName, ActivityKind.Internal);
//        return binder(_value);
//    }

//    public T Value => _value;
//}
