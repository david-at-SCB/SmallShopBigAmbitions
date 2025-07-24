namespace SmallShopBigAmbitions.Monads;

/// <summary>
/// An attempt at implementing a Haskell-like IO monad in C#.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <param name="Effect"></param>
public record class IO_dont_use<T>(Func<T> Effect)
{
    public T Run() => Effect();

    public IO_dont_use<R> Map<R>(Func<T, R> f) =>
        new(() => f(Run()));

    public IO_dont_use<R> Bind<R>(Func<T, IO_dont_use<R>> f) =>
        new(() => f(Run()).Run());

    public IO_dont_use<T> Tap(Action<T> action) =>
        new(() =>
        {
            var result = Run();
            action(result);
            return result;
        });

    public static IO_dont_use<Task<TResult>> FromTask<TResult>(Func<Task<TResult>> taskFactory) =>
        new(() => taskFactory());
}