namespace SmallShopBigAmbitions.Monads;

public record class IO<T>(Func<T> Effect)
{
    public T Run() => Effect();

    public IO<R> Map<R>(Func<T, R> f) =>
        new(() => f(Run()));

    public IO<R> Bind<R>(Func<T, IO<R>> f) =>
        new(() => f(Run()).Run());

    public IO<T> Tap(Action<T> action) =>
        new(() =>
        {
            var result = Run();
            action(result);
            return result;
        });

    public static IO<Task<TResult>> FromTask<TResult>(Func<Task<TResult>> taskFactory) =>
        new(() => taskFactory());

}